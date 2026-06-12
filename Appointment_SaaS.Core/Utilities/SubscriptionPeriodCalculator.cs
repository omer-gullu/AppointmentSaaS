using Appointment_SaaS.Core.Constants;

namespace Appointment_SaaS.Core.Utilities;

public static class SubscriptionPeriodCalculator
{
    public static DateTime CalculateEndDateFromPayment(DateTime paymentUtcOrLocal, string billingCycle, bool isTrial)
    {
        if (isTrial)
            return paymentUtcOrLocal.AddDays(15);

        return string.Equals(BillingCycles.Normalize(billingCycle), BillingCycles.Yearly, StringComparison.OrdinalIgnoreCase)
            ? paymentUtcOrLocal.AddYears(1)
            : paymentUtcOrLocal.AddMonths(1);
    }

    public static int GetDaysRemaining(DateTime subscriptionEndDate, DateTime? now = null)
    {
        var reference = now ?? DateTime.Now;
        var days = (int)Math.Ceiling((subscriptionEndDate.Date - reference.Date).TotalDays);
        return Math.Max(0, days);
    }

    /// <summary>Yeni ücretli dönemin gün sayısı (aylık/yıllık takvim süresi).</summary>
    public static int GetStandardPeriodDays(string? billingCycle, DateTime? from = null)
    {
        var start = (from ?? DateTime.Now).Date;
        var end = CalculateEndDateFromPayment(start, billingCycle ?? BillingCycles.Monthly, isTrial: false);
        return Math.Max(1, (end.Date - start).Days);
    }
}
