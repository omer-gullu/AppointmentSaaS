using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract;

/// <summary>
/// OTP ve oturum doğrulama için tenant + kullanıcı erişim kuralları (tek kaynak).
/// </summary>
public enum TenantAccessDenialKind
{
    None = 0,
    Blacklisted,
    Suspended,
    SubscriptionExpired,
    TrialExpired
}

public sealed record TenantAccessEvaluation(
    bool IsAllowed,
    TenantAccessDenialKind DenialKind,
    string? Message,
    int SuggestedStatusCode,
    bool ShouldDeactivateTenantForExpiredSubscription);

public interface ITenantAccessEvaluator
{
    TenantAccessEvaluation Evaluate(Tenant tenant, AppUser user);
}
