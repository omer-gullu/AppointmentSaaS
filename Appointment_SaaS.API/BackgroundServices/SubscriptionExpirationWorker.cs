using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Diagnostics;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using Appointment_SaaS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Appointment_SaaS.API.BackgroundServices;

public class SubscriptionExpirationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SubscriptionExpirationWorker> _logger;

    public SubscriptionExpirationWorker(IServiceProvider serviceProvider, ILogger<SubscriptionExpirationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Subscription Expiration Worker başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckExpirationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Abonelik bitiş kontrolü sırasında bir hata oluştu.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task CheckExpirationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
        var planService = scope.ServiceProvider.GetRequiredService<ITenantPlanService>();
        var iyzico = scope.ServiceProvider.GetRequiredService<IIyzicoPaymentService>();

        var scheduledApplied = await planService.ApplyDueScheduledPlanChangesAsync(cancellationToken);
        if (scheduledApplied > 0)
            _logger.LogInformation("{Count} planlanmış plan geçişi uygulandı.", scheduledApplied);

        var now = DateTime.UtcNow;
        var suspensionCutoff = now.AddHours(-SubscriptionAccessPolicy.RenewalGraceHours);

        var expiredTenants = await dbContext.Tenants
            .Where(t => t.IsActive && t.IsSubscriptionActive && !t.IsTrial
                        && t.SubscriptionEndDate < suspensionCutoff
                        && t.SubscriptionEndDate.Year > 2000)
            .ToListAsync(cancellationToken);

        if (expiredTenants.Any())
        {
            _logger.LogInformation("{Count} adet süresi dolmuş işletme bulundu.", expiredTenants.Count);

            foreach (var tenant in expiredTenants)
            {
                if (!string.IsNullOrWhiteSpace(tenant.PendingPlanType)
                    || !string.IsNullOrWhiteSpace(tenant.PendingCheckoutToken))
                {
                    AgentDebugLog.Write("H4", "SubscriptionExpirationWorker.CheckExpirations", "skip_pending_plan_change", new
                    {
                        tenant.TenantID,
                        tenant.PlanType,
                        tenant.BillingCycle,
                        pendingPlan = tenant.PendingPlanType,
                        endDate = tenant.SubscriptionEndDate.ToString("o")
                    });
                    continue;
                }

                if (await planService.TryReconcileFromIyzicoAsync(tenant, cancellationToken))
                {
                    await tenantService.UpdateAsync(tenant);
                    AgentDebugLog.Write("H1", "SubscriptionExpirationWorker.CheckExpirations", "reconciled_before_suspend", new
                    {
                        tenant.TenantID,
                        tenant.PlanType,
                        tenant.BillingCycle,
                        endDate = tenant.SubscriptionEndDate.ToString("o")
                    }, "post-fix");
                    continue;
                }

                var refreshed = await TryRefreshEndDateFromIyzicoAsync(tenant, iyzico, cancellationToken);
                if (refreshed && SubscriptionAccessPolicy.IsPaidSubscriptionOpen(tenant, now))
                {
                    await tenantService.UpdateAsync(tenant);
                    AgentDebugLog.Write("H1", "SubscriptionExpirationWorker.CheckExpirations", "end_date_refreshed_active", new
                    {
                        tenant.TenantID,
                        tenant.PlanType,
                        tenant.BillingCycle,
                        endDate = tenant.SubscriptionEndDate.ToString("o")
                    });
                    continue;
                }

                AgentDebugLog.Write("H1", "SubscriptionExpirationWorker.CheckExpirations", "suspend_expired", new
                {
                    tenant.TenantID,
                    tenant.PlanType,
                    tenant.BillingCycle,
                    endDate = tenant.SubscriptionEndDate.ToString("o"),
                    cancelAtPeriodEnd = tenant.CancelAtPeriodEnd,
                    refreshedAttempted = refreshed
                });

                tenant.CancelAtPeriodEnd = false;
                await tenantService.UpdateSubscriptionStatusAsync(tenant, false);
                _logger.LogInformation("İşletme askıya alındı: TenantId={TenantId}, Name={Name}", tenant.TenantID, tenant.Name);
            }
        }

        var expiredTrials = await dbContext.Tenants
            .Where(t => t.IsTrial && t.IsActive && t.AppUsers.Any(u =>
                u.TrialEndDate.HasValue && u.TrialEndDate.Value < now))
            .ToListAsync(cancellationToken);

        if (expiredTrials.Any())
        {
            _logger.LogInformation("{Count} adet deneme süresi dolmuş işletme askıya alınıyor...", expiredTrials.Count);
            foreach (var tenant in expiredTrials)
            {
                await tenantService.UpdateSubscriptionStatusAsync(tenant, false);
                _logger.LogInformation("Deneme süresi doldu — askıya alındı: TenantId={TenantId}", tenant.TenantID);
            }
        }

        if (!expiredTenants.Any() && !expiredTrials.Any() && scheduledApplied == 0)
            _logger.LogInformation("Süresi dolmuş işletme bulunamadı.");
    }

    private static async Task<bool> TryRefreshEndDateFromIyzicoAsync(
        Tenant tenant,
        IIyzicoPaymentService iyzico,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(tenant.SubscriptionReferenceCode))
            return false;

        try
        {
            var detail = await iyzico.GetSubscriptionDetailAsync(tenant.SubscriptionReferenceCode, cancellationToken);
            if (detail?.EndDate == null)
                return false;

            if (!string.Equals(detail.SubscriptionStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(detail.SubscriptionStatus, "UPGRADED", StringComparison.OrdinalIgnoreCase))
                return false;

            tenant.SubscriptionEndDate = detail.EndDate.Value;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
