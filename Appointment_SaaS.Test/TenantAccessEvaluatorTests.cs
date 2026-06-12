using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Appointment_SaaS.Test;

public class TenantAccessEvaluatorTests
{
    private readonly ITenantAccessEvaluator _evaluator = new TenantAccessEvaluator();

    public TenantAccessEvaluatorTests()
    {
        SubscriptionAccessPolicy.Configure(new SubscriptionBillingOptions { RenewalGraceHours = 12 });
    }

    [Fact]
    public void Evaluate_ShouldAllow_WhenPaidTenantActiveAndFutureEnd()
    {
        var tenant = new Tenant
        {
            IsBlacklisted = false,
            IsActive = true,
            IsSubscriptionActive = true,
            IsTrial = false,
            SubscriptionEndDate = DateTime.Now.AddDays(5)
        };
        var user = new AppUser { TrialEndDate = null };

        var r = _evaluator.Evaluate(tenant, user);

        r.IsAllowed.Should().BeTrue();
        r.ShouldDeactivateTenantForExpiredSubscription.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ShouldAllow_WhenPaidAndWithinRenewalGrace()
    {
        var end = DateTime.Now.AddHours(-2);
        var tenant = new Tenant
        {
            IsBlacklisted = false,
            IsActive = true,
            IsSubscriptionActive = true,
            IsTrial = false,
            SubscriptionEndDate = end
        };

        var r = _evaluator.Evaluate(tenant, new AppUser());

        r.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldDenySubscriptionExpired_WhenPaidAndPastGraceWindow()
    {
        var end = DateTime.Now.AddHours(-SubscriptionAccessPolicy.RenewalGraceHours - 1);
        var tenant = new Tenant
        {
            IsBlacklisted = false,
            IsActive = true,
            IsSubscriptionActive = true,
            IsTrial = false,
            SubscriptionEndDate = end
        };
        var user = new AppUser();

        var r = _evaluator.Evaluate(tenant, user);

        r.IsAllowed.Should().BeFalse();
        r.DenialKind.Should().Be(TenantAccessDenialKind.SubscriptionExpired);
        r.SuggestedStatusCode.Should().Be(StatusCodes.Status402PaymentRequired);
        r.ShouldDeactivateTenantForExpiredSubscription.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldIgnoreSubscriptionDate_WhenTrialTenant()
    {
        var tenant = new Tenant
        {
            IsBlacklisted = false,
            IsActive = true,
            IsSubscriptionActive = true,
            IsTrial = true,
            SubscriptionEndDate = DateTime.Now.AddDays(-30)
        };
        var user = new AppUser { TrialEndDate = DateTime.Now.AddDays(2) };

        var r = _evaluator.Evaluate(tenant, user);

        r.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldDenyTrial_WhenUserTrialEnded()
    {
        var tenant = new Tenant
        {
            IsBlacklisted = false,
            IsActive = true,
            IsSubscriptionActive = true,
            IsTrial = true,
            SubscriptionEndDate = DateTime.Now.AddDays(5)
        };
        var user = new AppUser { TrialEndDate = DateTime.Now.AddDays(-1) };

        var r = _evaluator.Evaluate(tenant, user);

        r.IsAllowed.Should().BeFalse();
        r.DenialKind.Should().Be(TenantAccessDenialKind.TrialExpired);
    }

    [Fact]
    public void Evaluate_ShouldIgnoreMinSubscriptionDate_WhenPaid()
    {
        var tenant = new Tenant
        {
            IsBlacklisted = false,
            IsActive = true,
            IsSubscriptionActive = true,
            IsTrial = false,
            SubscriptionEndDate = DateTime.MinValue
        };
        var user = new AppUser();

        var r = _evaluator.Evaluate(tenant, user);

        r.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldDeny_WhenBlacklisted()
    {
        var tenant = new Tenant
        {
            IsBlacklisted = true,
            IsActive = true,
            IsSubscriptionActive = true,
            IsTrial = false,
            SubscriptionEndDate = DateTime.Now.AddYears(1)
        };

        var r = _evaluator.Evaluate(tenant, new AppUser());

        r.IsAllowed.Should().BeFalse();
        r.DenialKind.Should().Be(TenantAccessDenialKind.Blacklisted);
    }

    [Fact]
    public void Evaluate_ShouldDeny_WhenSubscriptionInactive()
    {
        var tenant = new Tenant
        {
            IsBlacklisted = false,
            IsActive = true,
            IsSubscriptionActive = false,
            IsTrial = false,
            SubscriptionEndDate = DateTime.Now.AddYears(1)
        };

        var r = _evaluator.Evaluate(tenant, new AppUser());

        r.IsAllowed.Should().BeFalse();
        r.DenialKind.Should().Be(TenantAccessDenialKind.Suspended);
    }
}
