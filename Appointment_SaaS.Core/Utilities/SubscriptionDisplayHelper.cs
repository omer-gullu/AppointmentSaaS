using Appointment_SaaS.Core.Constants;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Core.Utilities;

public readonly record struct SubscriptionDaysDisplay(
    int CurrentPeriodDaysRemaining,
    int? ScheduledNewPlanDays,
    int TotalAccessDays,
    string DaysRemainingLabel,
    bool HasScheduledPlanActivation);

public static class SubscriptionDisplayHelper
{
    public static SubscriptionDaysDisplay Build(Tenant tenant)
    {
        var remaining = SubscriptionPeriodCalculator.GetDaysRemaining(tenant.SubscriptionEndDate);

        var hasScheduled = !tenant.IsTrial
            && !string.IsNullOrWhiteSpace(tenant.PendingPlanType)
            && string.IsNullOrWhiteSpace(tenant.PendingCheckoutToken)
            && tenant.PendingPlanEffectiveDate.HasValue
            && tenant.PendingPlanEffectiveDate.Value.Date > DateTime.Now.Date;

        if (!hasScheduled)
        {
            return new SubscriptionDaysDisplay(
                remaining,
                null,
                remaining,
                $"{remaining} gün kaldı",
                false);
        }

        var newPlanDays = SubscriptionPeriodCalculator.GetStandardPeriodDays(
            tenant.PendingBillingCycle ?? tenant.BillingCycle,
            tenant.PendingPlanEffectiveDate!.Value.Date);

        var total = remaining + newPlanDays;
        var label = $"({remaining} + {newPlanDays} gün)";

        return new SubscriptionDaysDisplay(
            remaining,
            newPlanDays,
            total,
            label,
            true);
    }

    public static string FormatBillingCycleLabel(string? cycle) =>
        BillingCycles.Normalize(cycle) == BillingCycles.Yearly ? "Yıllık" : "Aylık";
}
