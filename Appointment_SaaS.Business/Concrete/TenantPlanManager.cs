using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Diagnostics;
using Appointment_SaaS.Core.Constants;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using Appointment_SaaS.Data.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Appointment_SaaS.Business.Concrete;

public class TenantPlanManager : ITenantPlanService
{
    private readonly ITenantService _tenantService;
    private readonly IIyzicoPaymentService _iyzicoPaymentService;
    private readonly AppDbContext _db;
    private readonly ILogger<TenantPlanManager> _logger;

    public TenantPlanManager(
        ITenantService tenantService,
        IIyzicoPaymentService iyzicoPaymentService,
        AppDbContext db,
        ILogger<TenantPlanManager> logger)
    {
        _tenantService = tenantService;
        _iyzicoPaymentService = iyzicoPaymentService;
        _db = db;
        _logger = logger;
    }

    public async Task<ChangePlanInitResponseDto> InitializePlanChangeAsync(
        Tenant tenant,
        AppUser owner,
        ChangePlanInitRequestDto request,
        string callbackUrl)
    {
        var validation = PlanTransitionValidator.Validate(
            tenant.PlanType,
            tenant.BillingCycle,
            request.TargetPlanType,
            request.TargetBillingCycle,
            tenant.IsTrial);

        if (!validation.IsAllowed)
            throw new BadHttpRequestException(validation.Message ?? "Plan geçişi geçersiz.");

        if (!tenant.IsTrial
            && tenant.IsSubscriptionActive
            && !tenant.CancelAtPeriodEnd
            && !string.IsNullOrWhiteSpace(ResolveEffectiveSubscriptionReference(tenant)))
        {
            throw new BadHttpRequestException(
                "Yeni plan seçmeden önce mevcut aboneliğinizin otomatik yenilenmesini iptal etmeniz gerekir.");
        }

        var targetPlan = request.TargetPlanType.Trim();
        var targetCycle = BillingCycles.Normalize(request.TargetBillingCycle);
        var targetPricingRef = _iyzicoPaymentService.GetPricingPlanReferenceCode(targetPlan, targetCycle);

        var sameCycle = !PlanTransitionValidator.RequiresCheckoutForCycleChange(tenant.BillingCycle, targetCycle);
        var effectiveSubscriptionRef = ResolveEffectiveSubscriptionReference(tenant);
        var hasSubscription = !string.IsNullOrWhiteSpace(effectiveSubscriptionRef) && !tenant.IsTrial;
        var deferActivation = ShouldDeferPaidPlanActivation(tenant);

        AgentDebugLog.Write("H-D", "TenantPlanManager.InitializePlanChange", "branch", new
        {
            tenant.TenantID,
            tenant.PlanType,
            tenant.BillingCycle,
            tenant.IsTrial,
            targetPlan,
            targetCycle,
            sameCycle,
            hasSubscription,
            deferActivation,
            hasPending = !string.IsNullOrWhiteSpace(tenant.PendingPlanType)
        });

        if (hasSubscription && sameCycle)
        {
            await _iyzicoPaymentService.UpgradeSubscriptionAsync(
                effectiveSubscriptionRef!,
                targetPricingRef);

            tenant.PendingPlanType = targetPlan;
            tenant.PendingBillingCycle = targetCycle;

            if (deferActivation)
            {
                SchedulePendingPlanActivation(tenant);
                await _tenantService.UpdateAsync(tenant);
                return BuildScheduledInitResponse(tenant);
            }

            await ApplyPendingPlanAsync(tenant, fromUpgrade: true, forceImmediate: true);
            await _tenantService.UpdateAsync(tenant);

            return new ChangePlanInitResponseDto
            {
                Mode = "upgrade",
                Message = "Planınız İyzico üzerinden güncellendi."
            };
        }

        if (hasSubscription)
        {
            var currentRef = ResolveEffectiveSubscriptionReference(tenant);
            if (!string.IsNullOrWhiteSpace(currentRef))
                tenant.PreviousSubscriptionReferenceCode = currentRef;
        }

        var init = await _iyzicoPaymentService.InitializeSubscriptionCheckoutFormAsync(
            tenant,
            owner.Email,
            $"{owner.FirstName} {owner.LastName}".Trim(),
            owner.PhoneNumber,
            "11111111111",
            targetPlan,
            targetCycle,
            callbackUrl);

        tenant.PendingPlanType = targetPlan;
        tenant.PendingBillingCycle = targetCycle;
        tenant.PendingCheckoutToken = init.Token;
        tenant.PendingPlanEffectiveDate = null;
        await _tenantService.UpdateAsync(tenant);

        var checkoutMessage = deferActivation
            ? $"Ödeme formu hazır. Mevcut planınız {tenant.SubscriptionEndDate:dd.MM.yyyy} tarihine kadar geçerlidir; yeni plan bu tarihten sonra aktif olur."
            : "Ödeme formu hazır. Ödeme tamamlanmadan plan değişmez.";

        return new ChangePlanInitResponseDto
        {
            Mode = "checkout",
            CheckoutFormContent = init.CheckoutFormContent,
            PendingToken = init.Token,
            Message = checkoutMessage
        };
    }

    public async Task<bool> CompletePlanChangePaymentAsync(Tenant tenant, string checkoutToken)
    {
        var pricingRef = _iyzicoPaymentService.GetPricingPlanReferenceCode(
            tenant.PendingPlanType ?? tenant.PlanType,
            tenant.PendingBillingCycle ?? tenant.BillingCycle);

        var refsToRetire = CollectSubscriptionRefsToRetire(tenant);

        var verify = await _iyzicoPaymentService.VerifyCheckoutFormAsync(
            checkoutToken,
            pricingRef,
            excludeSubscriptionReferenceCode: tenant.PreviousSubscriptionReferenceCode);

        if (!string.IsNullOrWhiteSpace(verify.SubscriptionReferenceCode))
            tenant.SubscriptionReferenceCode = verify.SubscriptionReferenceCode;
        if (!string.IsNullOrWhiteSpace(verify.CustomerReferenceCode))
            tenant.IyzicoUserKey = verify.CustomerReferenceCode;

        await CancelSupersededIyzicoSubscriptionsAsync(
            tenant,
            verify.SubscriptionReferenceCode,
            refsToRetire);

        tenant.PreviousSubscriptionReferenceCode = null;
        tenant.PendingCheckoutToken = null;
        tenant.IsActive = true;
        tenant.IsSubscriptionActive = true;
        tenant.CancelAtPeriodEnd = false;

        if (ShouldDeferPaidPlanActivation(tenant))
        {
            SchedulePendingPlanActivation(tenant);
            await _tenantService.UpdateSubscriptionStatusAsync(tenant, true);
            await _tenantService.UpdateAsync(tenant);

            _logger.LogInformation(
                "Plan ödemesi alındı; eski İyzico abonelikleri iptal edildi. Aktivasyon {EffectiveDate:yyyy-MM-dd}. TenantId={Id}",
                tenant.PendingPlanEffectiveDate, tenant.TenantID);
            return true;
        }

        await ApplyPendingPlanAsync(tenant, fromUpgrade: false, forceImmediate: true);
        await _tenantService.UpdateSubscriptionStatusAsync(tenant, true);
        await _tenantService.UpdateAsync(tenant);

        AgentDebugLog.Write("H2", "TenantPlanManager.CompletePlanChangePayment", "completed", new
        {
            tenant.TenantID,
            tenant.PlanType,
            tenant.BillingCycle,
            endDate = tenant.SubscriptionEndDate.ToString("o"),
            subRefLen = tenant.SubscriptionReferenceCode?.Length ?? 0
        });

        _logger.LogInformation("Plan ödemesi tamamlandı. TenantId={Id} Plan={Plan}", tenant.TenantID, tenant.PlanType);
        return true;
    }

    public async Task ApplySubscriptionFromWebhookAsync(
        Tenant tenant,
        string? paymentId,
        string? rawBody,
        bool isSuccess,
        bool isFailure,
        bool isUpgrade)
    {
        if (isFailure)
        {
            var hadPendingPlanChange = !string.IsNullOrWhiteSpace(tenant.PendingPlanType);
            var previousRef = tenant.PreviousSubscriptionReferenceCode;

            tenant.PendingPlanType = null;
            tenant.PendingBillingCycle = null;
            tenant.PendingCheckoutToken = null;
            tenant.PendingPlanEffectiveDate = null;
            tenant.PreviousSubscriptionReferenceCode = null;

            if (hadPendingPlanChange)
            {
                if (!string.IsNullOrWhiteSpace(previousRef))
                    tenant.SubscriptionReferenceCode = previousRef;

                AgentDebugLog.Write("H-A", "TenantPlanManager.ApplySubscriptionFromWebhook",
                    "payment_failed_pending_only", new
                    {
                        tenant.TenantID,
                        restoredRef = !string.IsNullOrWhiteSpace(previousRef),
                        tenant.IsTrial
                    }, "post-fix");

                await _tenantService.UpdateAsync(tenant);
                return;
            }

            AgentDebugLog.Write("H-A", "TenantPlanManager.ApplySubscriptionFromWebhook",
                "payment_failed_full_suspend", new { tenant.TenantID, tenant.PlanType }, "post-fix");

            await _tenantService.UpdateSubscriptionStatusAsync(tenant, false);
            await _tenantService.UpdateAsync(tenant);
            return;
        }

        if (!isSuccess)
            return;

        if (!string.IsNullOrWhiteSpace(tenant.SubscriptionReferenceCode))
        {
            var detail = await _iyzicoPaymentService.GetSubscriptionDetailAsync(tenant.SubscriptionReferenceCode);
            if (detail?.EndDate != null && !HasScheduledFutureActivation(tenant))
                tenant.SubscriptionEndDate = detail.EndDate.Value;
        }

        if (!string.IsNullOrWhiteSpace(tenant.PendingPlanType))
        {
            if (ShouldDeferPaidPlanActivation(tenant) || HasScheduledFutureActivation(tenant))
            {
                if (!tenant.PendingPlanEffectiveDate.HasValue)
                    SchedulePendingPlanActivation(tenant);
            }
            else
            {
                await ApplyPendingPlanAsync(tenant, fromUpgrade: isUpgrade, forceImmediate: true);
            }
        }

        tenant.IsActive = true;
        tenant.IsSubscriptionActive = true;
        tenant.PendingCheckoutToken = null;
        tenant.CancelAtPeriodEnd = false;
        await _tenantService.UpdateSubscriptionStatusAsync(tenant, true);
        await _tenantService.UpdateAsync(tenant);
    }

    public async Task<int> ApplyDueScheduledPlanChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dueTenants = await _db.Tenants
            .Where(t => !t.IsTrial
                        && t.PendingPlanType != null
                        && t.PendingPlanEffectiveDate != null
                        && t.PendingPlanEffectiveDate <= now)
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var tenant in dueTenants)
        {
            if (await TryActivateScheduledPlanAsync(tenant, cancellationToken))
                count++;
        }

        return count;
    }

    private async Task<bool> TryActivateScheduledPlanAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenant.PendingPlanType)
            || !tenant.PendingPlanEffectiveDate.HasValue
            || tenant.PendingPlanEffectiveDate.Value > DateTime.Now)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(tenant.PreviousSubscriptionReferenceCode)
            && !string.Equals(
                tenant.PreviousSubscriptionReferenceCode,
                tenant.SubscriptionReferenceCode,
                StringComparison.Ordinal))
        {
            try
            {
                await _iyzicoPaymentService.CancelSubscriptionAsync(
                    tenant.PreviousSubscriptionReferenceCode,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Planlanmış geçiş: eski abonelik iptal edilemedi. TenantId={Id}", tenant.TenantID);
            }
        }

        tenant.PreviousSubscriptionReferenceCode = null;
        await ApplyPendingPlanAsync(tenant, fromUpgrade: false, forceImmediate: true);
        tenant.PendingPlanEffectiveDate = null;
        tenant.PendingCheckoutToken = null;
        tenant.CancelAtPeriodEnd = false;
        tenant.IsActive = true;
        tenant.IsSubscriptionActive = true;
        await _tenantService.UpdateSubscriptionStatusAsync(tenant, true);
        await _tenantService.UpdateAsync(tenant);

        _logger.LogInformation(
            "Planlanmış plan geçişi uygulandı. TenantId={Id} Plan={Plan} Bitiş={End}",
            tenant.TenantID, tenant.PlanType, tenant.SubscriptionEndDate);

        return true;
    }

    private async Task ApplyPendingPlanAsync(Tenant tenant, bool fromUpgrade, bool forceImmediate)
    {
        if (string.IsNullOrWhiteSpace(tenant.PendingPlanType))
            return;

        if (!forceImmediate && ShouldDeferPaidPlanActivation(tenant))
            return;

        var targetPlan = tenant.PendingPlanType;
        var targetCycle = BillingCycles.Normalize(tenant.PendingBillingCycle ?? tenant.BillingCycle);

        tenant.PlanType = targetPlan;
        tenant.BillingCycle = targetCycle;
        tenant.IsTrial = false;
        tenant.TrialUsed = true;
        tenant.PendingPlanType = null;
        tenant.PendingBillingCycle = null;
        tenant.PendingPlanEffectiveDate = null;

        if (!string.IsNullOrWhiteSpace(tenant.SubscriptionReferenceCode))
        {
            var detail = await _iyzicoPaymentService.GetSubscriptionDetailAsync(tenant.SubscriptionReferenceCode);
            if (detail?.EndDate != null && detail.EndDate.Value >= DateTime.Now)
            {
                tenant.SubscriptionEndDate = detail.EndDate.Value;
            }
            else
            {
                tenant.SubscriptionEndDate = SubscriptionPeriodCalculator.CalculateEndDateFromPayment(
                    DateTime.Now, tenant.BillingCycle, isTrial: false);
            }
        }
        else
        {
            tenant.SubscriptionEndDate = SubscriptionPeriodCalculator.CalculateEndDateFromPayment(
                DateTime.Now, tenant.BillingCycle, isTrial: false);
        }

        await Task.CompletedTask;
    }

    public async Task<bool> TryReconcileFromIyzicoAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        if (HasScheduledFutureActivation(tenant))
            return false;

        AgentDebugLog.Write("H7", "TenantPlanManager.TryReconcileFromIyzico", "entry", new
        {
            tenant.TenantID,
            tenant.IsActive,
            tenant.IsSubscriptionActive,
            tenant.PlanType,
            tenant.BillingCycle,
            hasPending = !string.IsNullOrWhiteSpace(tenant.PendingPlanType),
            endDate = tenant.SubscriptionEndDate.ToString("o"),
            hasCustomerKey = !string.IsNullOrWhiteSpace(tenant.IyzicoUserKey)
        }, "post-fix");

        var storedEndLooksValid = tenant.SubscriptionEndDate >= DateTime.Now
            && !IsSuspiciousSubscriptionEndDate(tenant.SubscriptionEndDate, tenant.BillingCycle);

        if (tenant.IsActive && tenant.IsSubscriptionActive
            && storedEndLooksValid
            && string.IsNullOrWhiteSpace(tenant.PendingPlanType))
        {
            return false;
        }

        if (tenant.IsActive
            && IsSuspiciousSubscriptionEndDate(tenant.SubscriptionEndDate, tenant.BillingCycle))
        {
            AgentDebugLog.Write("H10", "TenantPlanManager.TryReconcileFromIyzico", "reconcile_suspicious_stored_end", new
            {
                tenant.TenantID,
                tenant.BillingCycle,
                endDate = tenant.SubscriptionEndDate.ToString("o"),
                daysRemaining = (tenant.SubscriptionEndDate.Date - DateTime.Now.Date).Days
            }, "post-fix");
        }

        var refsToTry = new List<string>();
        if (!string.IsNullOrWhiteSpace(tenant.SubscriptionReferenceCode)
            && !string.Equals(tenant.SubscriptionReferenceCode, tenant.PendingCheckoutToken, StringComparison.Ordinal))
        {
            refsToTry.Add(tenant.SubscriptionReferenceCode);
        }

        if (!string.IsNullOrWhiteSpace(tenant.PreviousSubscriptionReferenceCode))
            refsToTry.Add(tenant.PreviousSubscriptionReferenceCode);

        var skippedSuspiciousKnownRef = false;
        foreach (var refCode in refsToTry.Distinct(StringComparer.Ordinal))
        {
            var detail = await _iyzicoPaymentService.GetSubscriptionDetailAsync(refCode, cancellationToken);
            if (detail?.EndDate != null
                && IsSuspiciousSubscriptionEndDate(detail.EndDate.Value, tenant.BillingCycle, detail.PricingPlanReferenceCode))
            {
                skippedSuspiciousKnownRef = true;
                AgentDebugLog.Write("H9", "TenantPlanManager.TryReconcileFromIyzico", "skip_suspicious_known_ref", new
                {
                    tenant.TenantID,
                    tenant.BillingCycle,
                    endDate = detail.EndDate.Value.ToString("o"),
                    daysRemaining = (detail.EndDate.Value.Date - DateTime.Now.Date).Days,
                    refCodeLen = refCode.Length
                }, "post-fix");
                continue;
            }

            if (await TryApplyReconciledDetailAsync(tenant, detail, "known_ref", cancellationToken))
                return true;
        }

        var preferRef = skippedSuspiciousKnownRef ? null : tenant.SubscriptionReferenceCode;

        if (!string.IsNullOrWhiteSpace(tenant.PendingPlanType)
            && !string.IsNullOrWhiteSpace(tenant.PendingBillingCycle)
            && !HasScheduledFutureActivation(tenant))
        {
            var pendingPricingRef = _iyzicoPaymentService.GetPricingPlanReferenceCode(
                tenant.PendingPlanType, tenant.PendingBillingCycle);
            var pendingDetail = await _iyzicoPaymentService.ResolveLatestActiveSubscriptionAsync(
                pendingPricingRef,
                tenant.PreviousSubscriptionReferenceCode,
                preferRef,
                cancellationToken);
            if (await TryApplyReconciledDetailAsync(tenant, pendingDetail, "pending_pricing_search", cancellationToken))
                return true;
        }

        if (!string.IsNullOrWhiteSpace(tenant.IyzicoUserKey))
        {
            var byCustomer = await _iyzicoPaymentService.ResolveLatestActiveByCustomerAsync(
                tenant.IyzicoUserKey,
                preferRef,
                cancellationToken);
            if (await TryApplyReconciledDetailAsync(tenant, byCustomer, "customer_search", cancellationToken))
                return true;
        }

        foreach (var (plan, cycle) in BuildReconcilePlanCycleCandidates(tenant))
        {
            string pricingRef;
            try
            {
                pricingRef = _iyzicoPaymentService.GetPricingPlanReferenceCode(plan, cycle);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(pricingRef))
                continue;

            var detail = await _iyzicoPaymentService.ResolveLatestActiveSubscriptionAsync(
                pricingRef,
                tenant.PreviousSubscriptionReferenceCode,
                preferRef,
                cancellationToken);
            if (await TryApplyReconciledDetailAsync(tenant, detail, $"pricing_sweep_{plan}_{cycle}", cancellationToken))
                return true;
        }

        AgentDebugLog.Write("H7", "TenantPlanManager.TryReconcileFromIyzico", "failed_no_active_sub", new
        {
            tenant.TenantID,
            tenant.PlanType,
            tenant.BillingCycle
        }, "post-fix");

        return false;
    }

    private static IEnumerable<(string Plan, string Cycle)> BuildReconcilePlanCycleCandidates(Tenant tenant)
    {
        var plan = string.IsNullOrWhiteSpace(tenant.PlanType) ? "Pro" : tenant.PlanType;
        var cycle = BillingCycles.Normalize(tenant.BillingCycle);
        yield return (plan, cycle);
        yield return (plan, BillingCycles.Yearly);
        yield return (plan, BillingCycles.Monthly);
        if (!string.Equals(plan, "Pro", StringComparison.OrdinalIgnoreCase))
            yield return ("Pro", BillingCycles.Yearly);
    }

    private static bool IsSuspiciousSubscriptionEndDate(
        DateTime endDate,
        string? billingCycle,
        string? pricingPlanReferenceCode = null)
    {
        var days = (endDate.Date - DateTime.Now.Date).Days;
        if (days < 0)
            return false;

        var cycle = BillingCycles.Normalize(billingCycle);
        if (cycle == BillingCycles.Yearly)
            return days > 400;

        if (cycle == BillingCycles.Monthly)
            return days > 35;

        return days > 400;
    }

    private async Task<bool> TryApplyReconciledDetailAsync(
        Tenant tenant,
        IyzicoSubscriptionDetailResult? detail,
        string source,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        if (detail?.EndDate == null || detail.EndDate < DateTime.Now)
            return false;

        if (IsSuspiciousSubscriptionEndDate(
                detail.EndDate.Value,
                tenant.PendingBillingCycle ?? tenant.BillingCycle,
                detail.PricingPlanReferenceCode))
        {
            AgentDebugLog.Write("H9", "TenantPlanManager.TryApplyReconciledDetail", "reject_suspicious_end", new
            {
                tenant.TenantID,
                source,
                endDate = detail.EndDate.Value.ToString("o"),
                daysRemaining = (detail.EndDate.Value.Date - DateTime.Now.Date).Days
            }, "post-fix");
            return false;
        }

        var status = detail.SubscriptionStatus ?? string.Empty;
        if (!string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "UPGRADED", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        tenant.SubscriptionReferenceCode = detail.SubscriptionReferenceCode;
        tenant.SubscriptionEndDate = detail.EndDate.Value;

        if (!string.IsNullOrWhiteSpace(tenant.PendingPlanType) && !HasScheduledFutureActivation(tenant))
        {
            tenant.PlanType = tenant.PendingPlanType;
            tenant.BillingCycle = BillingCycles.Normalize(tenant.PendingBillingCycle ?? tenant.BillingCycle);
            tenant.PendingPlanType = null;
            tenant.PendingBillingCycle = null;
            tenant.PendingCheckoutToken = null;
            tenant.PendingPlanEffectiveDate = null;
            tenant.PreviousSubscriptionReferenceCode = null;
            tenant.IsTrial = false;
        }
        else if (!string.IsNullOrWhiteSpace(detail.PricingPlanReferenceCode))
        {
            ApplyPlanFromPricingReference(tenant, detail.PricingPlanReferenceCode);
        }

        tenant.IsActive = true;
        tenant.IsSubscriptionActive = true;
        await _tenantService.UpdateSubscriptionStatusAsync(tenant, true);
        await _tenantService.UpdateAsync(tenant);

        AgentDebugLog.Write("H1", "TenantPlanManager.TryReconcileFromIyzico", "reactivated_from_iyzico", new
        {
            tenant.TenantID,
            tenant.PlanType,
            tenant.BillingCycle,
            endDate = tenant.SubscriptionEndDate.ToString("o"),
            iyzicoStatus = status,
            source
        }, "post-fix");

        return true;
    }

    private void ApplyPlanFromPricingReference(Tenant tenant, string pricingPlanReferenceCode)
    {
        foreach (var (plan, cycle) in new[]
                 {
                     ("Starter", BillingCycles.Monthly),
                     ("Starter", BillingCycles.Yearly),
                     ("Pro", BillingCycles.Monthly),
                     ("Pro", BillingCycles.Yearly),
                     ("Business", BillingCycles.Monthly),
                     ("Business", BillingCycles.Yearly),
                 })
        {
            string code;
            try
            {
                code = _iyzicoPaymentService.GetPricingPlanReferenceCode(plan, cycle);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(code)
                || !string.Equals(code, pricingPlanReferenceCode, StringComparison.Ordinal))
            {
                continue;
            }

            tenant.PlanType = plan;
            tenant.BillingCycle = cycle;
            tenant.IsTrial = false;
            return;
        }
    }

    private static string? ResolveEffectiveSubscriptionReference(Tenant tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant.SubscriptionReferenceCode)
            && !string.Equals(tenant.SubscriptionReferenceCode, tenant.PendingCheckoutToken, StringComparison.Ordinal))
        {
            return tenant.SubscriptionReferenceCode;
        }

        return !string.IsNullOrWhiteSpace(tenant.PreviousSubscriptionReferenceCode)
            ? tenant.PreviousSubscriptionReferenceCode
            : tenant.SubscriptionReferenceCode;
    }

    private static bool ShouldDeferPaidPlanActivation(Tenant tenant) =>
        !tenant.IsTrial
        && tenant.SubscriptionEndDate.Date > DateTime.Now.Date;

    private static bool HasScheduledFutureActivation(Tenant tenant) =>
        !string.IsNullOrWhiteSpace(tenant.PendingPlanType)
        && tenant.PendingPlanEffectiveDate.HasValue
        && tenant.PendingPlanEffectiveDate.Value.Date > DateTime.Now.Date;

    private static void SchedulePendingPlanActivation(Tenant tenant)
    {
        tenant.PendingPlanEffectiveDate = tenant.SubscriptionEndDate.Date;
    }

    private static ChangePlanInitResponseDto BuildScheduledInitResponse(Tenant tenant)
    {
        var display = SubscriptionDisplayHelper.Build(tenant);
        return new ChangePlanInitResponseDto
        {
            Mode = "scheduled",
            Message =
                $"Plan değişikliği kaydedildi. Mevcut planınız {tenant.SubscriptionEndDate:dd.MM.yyyy} tarihine kadar geçerlidir. " +
                $"Yeni plan bu tarihten itibaren aktif olur (toplam erişim: {display.DaysRemainingLabel})."
        };
    }

    private static List<string> CollectSubscriptionRefsToRetire(Tenant tenant)
    {
        var refs = new HashSet<string>(StringComparer.Ordinal);

        void AddIfValid(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)
                || string.Equals(code, tenant.PendingCheckoutToken, StringComparison.Ordinal))
            {
                return;
            }

            refs.Add(code);
        }

        AddIfValid(tenant.SubscriptionReferenceCode);
        AddIfValid(tenant.PreviousSubscriptionReferenceCode);

        return refs.ToList();
    }

    private async Task CancelSupersededIyzicoSubscriptionsAsync(
        Tenant tenant,
        string? keepSubscriptionReferenceCode,
        IReadOnlyCollection<string> refsCapturedBeforeUpdate,
        CancellationToken cancellationToken = default)
    {
        var refsToCancel = new HashSet<string>(StringComparer.Ordinal);

        foreach (var code in refsCapturedBeforeUpdate)
        {
            if (!string.IsNullOrWhiteSpace(code))
                refsToCancel.Add(code);
        }

        if (!string.IsNullOrWhiteSpace(tenant.PreviousSubscriptionReferenceCode))
            refsToCancel.Add(tenant.PreviousSubscriptionReferenceCode);

        if (!string.IsNullOrWhiteSpace(keepSubscriptionReferenceCode))
            refsToCancel.Remove(keepSubscriptionReferenceCode);

        if (!string.IsNullOrWhiteSpace(tenant.SubscriptionReferenceCode))
            refsToCancel.Remove(tenant.SubscriptionReferenceCode);

        foreach (var refCode in refsToCancel)
        {
            try
            {
                await _iyzicoPaymentService.CancelSubscriptionAsync(refCode, cancellationToken);
                _logger.LogInformation(
                    "Plan geçişi: eski İyzico aboneliği iptal edildi. TenantId={Id} RefLen={Len}",
                    tenant.TenantID, refCode.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Plan geçişi: eski abonelik iptal edilemedi. TenantId={Id} RefLen={Len}",
                    tenant.TenantID, refCode.Length);
            }
        }
    }
}
