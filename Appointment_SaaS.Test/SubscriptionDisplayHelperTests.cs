using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using FluentAssertions;

namespace Appointment_SaaS.Test;

public class SubscriptionDisplayHelperTests
{
    [Fact]
    public void Build_ShouldShowSumFormat_WhenScheduledPaidPlanChange()
    {
        var tenant = new Tenant
        {
            IsTrial = false,
            SubscriptionEndDate = DateTime.Now.Date.AddDays(10),
            PlanType = "Starter",
            BillingCycle = "Yearly",
            PendingPlanType = "Pro",
            PendingBillingCycle = "Monthly",
            PendingPlanEffectiveDate = DateTime.Now.Date.AddDays(10),
            PendingCheckoutToken = null
        };

        var display = SubscriptionDisplayHelper.Build(tenant);

        display.HasScheduledPlanActivation.Should().BeTrue();
        display.CurrentPeriodDaysRemaining.Should().Be(10);
        display.ScheduledNewPlanDays.Should().BeGreaterThan(27);
        display.DaysRemainingLabel.Should().StartWith("(").And.Contain("+").And.Contain("gün)");
        display.TotalAccessDays.Should().Be(display.CurrentPeriodDaysRemaining + display.ScheduledNewPlanDays!.Value);
    }

    [Fact]
    public void Build_ShouldShowSimpleLabel_WhenNoScheduledChange()
    {
        var tenant = new Tenant
        {
            IsTrial = false,
            SubscriptionEndDate = DateTime.Now.Date.AddDays(5),
            PlanType = "Pro",
            BillingCycle = "Monthly"
        };

        var display = SubscriptionDisplayHelper.Build(tenant);

        display.HasScheduledPlanActivation.Should().BeFalse();
        display.DaysRemainingLabel.Should().Be("5 gün kaldı");
    }
}
