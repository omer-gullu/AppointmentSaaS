using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Diagnostics;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using Microsoft.AspNetCore.Http;

namespace Appointment_SaaS.Business.Concrete;

public sealed class TenantAccessEvaluator : ITenantAccessEvaluator
{
    public TenantAccessEvaluation Evaluate(Tenant tenant, AppUser user)
    {
        if (tenant.IsBlacklisted)
        {
            return new TenantAccessEvaluation(
                false,
                TenantAccessDenialKind.Blacklisted,
                "Hesabınız kullanıma kapatılmıştır.",
                StatusCodes.Status403Forbidden,
                false);
        }

        if (!tenant.IsActive || !tenant.IsSubscriptionActive)
        {
            AgentDebugLog.Write("H5", "TenantAccessEvaluator.Evaluate", "suspended_flags", new
            {
                tenant.TenantID,
                tenant.IsActive,
                tenant.IsSubscriptionActive,
                tenant.PlanType,
                tenant.BillingCycle,
                hasPending = !string.IsNullOrWhiteSpace(tenant.PendingPlanType),
                endDate = tenant.SubscriptionEndDate.ToString("o")
            });

            return new TenantAccessEvaluation(
                false,
                TenantAccessDenialKind.Suspended,
                "Hesabınız askıya alınmıştır. Lütfen yöneticiyle iletişime geçin.",
                StatusCodes.Status403Forbidden,
                false);
        }

        if (!string.IsNullOrWhiteSpace(tenant.PendingPlanType)
            || !string.IsNullOrWhiteSpace(tenant.PendingCheckoutToken))
        {
            return new TenantAccessEvaluation(true, TenantAccessDenialKind.None, null, StatusCodes.Status200OK, false);
        }

        if (!tenant.IsTrial
            && tenant.SubscriptionEndDate != DateTime.MinValue
            && !SubscriptionAccessPolicy.IsPaidSubscriptionOpen(tenant))
        {
            var msg =
                $"İşletmenizin aboneliği {tenant.SubscriptionEndDate:dd.MM.yyyy HH:mm} tarihinde sona ermiştir.";
            return new TenantAccessEvaluation(
                false,
                TenantAccessDenialKind.SubscriptionExpired,
                msg,
                StatusCodes.Status402PaymentRequired,
                tenant.IsActive);
        }

        if (tenant.IsTrial
            && user.TrialEndDate.HasValue
            && user.TrialEndDate.Value < DateTime.Now)
        {
            return new TenantAccessEvaluation(
                false,
                TenantAccessDenialKind.TrialExpired,
                "İşletmenizin deneme süresi dolmuştur. Lütfen aboneliğinizi yükseltin.",
                StatusCodes.Status402PaymentRequired,
                false);
        }

        return new TenantAccessEvaluation(true, TenantAccessDenialKind.None, null, StatusCodes.Status200OK, false);
    }
}
