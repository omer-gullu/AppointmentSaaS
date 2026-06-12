using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Core.Utilities;

public static class SubscriptionAccessPolicy
{
    private static int _renewalGraceHours = 12;

    public static void Configure(SubscriptionBillingOptions options)
    {
        _renewalGraceHours = options.RenewalGraceHours > 0 ? options.RenewalGraceHours : 12;
    }

    public static int RenewalGraceHours => _renewalGraceHours;

    /// <summary>Ücretli erişim son anı: kayıtlı bitiş + grace (DB'ye yazılmaz).</summary>
    public static DateTime GetAccessUntil(Tenant tenant)
    {
        if (tenant.SubscriptionEndDate == DateTime.MinValue)
            return DateTime.MaxValue;

        return tenant.SubscriptionEndDate.AddHours(_renewalGraceHours);
    }

    public static bool IsPaidSubscriptionOpen(Tenant tenant, DateTime? now = null)
    {
        var reference = now ?? DateTime.Now;
        if (tenant.IsTrial)
            return true;

        if (tenant.SubscriptionEndDate == DateTime.MinValue)
            return true;

        return reference < GetAccessUntil(tenant);
    }

    /// <summary>Grace penceresinde İyzico'dan güncel dönem bitişi çekilmeli mi?</summary>
    public static bool ShouldAttemptIyzicoReconcile(Tenant tenant, DateTime? now = null)
    {
        var reference = now ?? DateTime.Now;
        if (tenant.IsTrial || !tenant.IsSubscriptionActive)
            return false;

        if (string.IsNullOrWhiteSpace(tenant.SubscriptionReferenceCode))
            return false;

        if (tenant.SubscriptionEndDate == DateTime.MinValue)
            return false;

        if (!string.IsNullOrWhiteSpace(tenant.PendingPlanType)
            || !string.IsNullOrWhiteSpace(tenant.PendingCheckoutToken))
            return false;

        return tenant.SubscriptionEndDate <= reference
               && reference < GetAccessUntil(tenant);
    }
}
