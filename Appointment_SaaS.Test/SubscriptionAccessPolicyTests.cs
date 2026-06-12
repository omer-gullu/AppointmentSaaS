using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using FluentAssertions;
using Xunit;

namespace Appointment_SaaS.Test;

public class SubscriptionAccessPolicyTests
{
    public SubscriptionAccessPolicyTests()
    {
        SubscriptionAccessPolicy.Configure(new SubscriptionBillingOptions { RenewalGraceHours = 12 });
    }

    [Fact]
    public void IsPaidSubscriptionOpen_ShouldBeTrue_WithinGraceAfterEnd()
    {
        var now = new DateTime(2026, 5, 21, 10, 0, 0);
        var tenant = new Tenant
        {
            IsTrial = false,
            SubscriptionEndDate = now.AddHours(-3)
        };

        SubscriptionAccessPolicy.IsPaidSubscriptionOpen(tenant, now).Should().BeTrue();
    }

    [Fact]
    public void IsPaidSubscriptionOpen_ShouldBeFalse_AfterGraceExpires()
    {
        var now = new DateTime(2026, 5, 21, 10, 0, 0);
        var tenant = new Tenant
        {
            IsTrial = false,
            SubscriptionEndDate = now.AddHours(-13)
        };

        SubscriptionAccessPolicy.IsPaidSubscriptionOpen(tenant, now).Should().BeFalse();
    }

    [Fact]
    public void ShouldAttemptIyzicoReconcile_ShouldBeTrue_InGraceWindow()
    {
        var now = new DateTime(2026, 5, 21, 10, 0, 0);
        var tenant = new Tenant
        {
            IsTrial = false,
            IsSubscriptionActive = true,
            SubscriptionReferenceCode = "SUB-REF",
            SubscriptionEndDate = now.AddHours(-1)
        };

        SubscriptionAccessPolicy.ShouldAttemptIyzicoReconcile(tenant, now).Should().BeTrue();
    }

    [Fact]
    public void ShouldAttemptIyzicoReconcile_ShouldBeFalse_WhenPendingPlanChange()
    {
        var now = new DateTime(2026, 5, 21, 10, 0, 0);
        var tenant = new Tenant
        {
            IsTrial = false,
            IsSubscriptionActive = true,
            SubscriptionReferenceCode = "SUB-REF",
            SubscriptionEndDate = now.AddHours(-1),
            PendingPlanType = "Pro"
        };

        SubscriptionAccessPolicy.ShouldAttemptIyzicoReconcile(tenant, now).Should().BeFalse();
    }
}
