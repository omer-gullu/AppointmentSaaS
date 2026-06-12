using Appointment_SaaS.Core.Utilities;
using FluentAssertions;
using Xunit;

namespace Appointment_SaaS.Test;

public class PlanTransitionValidatorTests
{
    [Theory]
    [InlineData("Trial", "Monthly", "Starter", "Monthly", true)]
    [InlineData("Trial", "Monthly", "Pro", "Yearly", true)]
    [InlineData("Pro", "Monthly", "Starter", "Monthly", true)]
    [InlineData("Pro", "Monthly", "Business", "Yearly", true)]
    [InlineData("Starter", "Yearly", "Pro", "Yearly", true)]
    public void Validate_AllowedTransitions(string currentPlan, string currentCycle, string targetPlan, string targetCycle, bool isTrial)
    {
        var result = PlanTransitionValidator.Validate(currentPlan, currentCycle, targetPlan, targetCycle, isTrial);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Validate_PaidToTrial_IsBlocked()
    {
        var result = PlanTransitionValidator.Validate("Pro", "Monthly", "Trial", "Monthly", isTrial: false);
        result.IsAllowed.Should().BeFalse();
        result.Kind.Should().Be(PlanTransitionKind.DowngradeToTrialNotAllowed);
    }

    [Fact]
    public void Validate_SamePlan_IsBlocked()
    {
        var result = PlanTransitionValidator.Validate("Pro", "Monthly", "Pro", "Monthly", isTrial: false);
        result.IsAllowed.Should().BeFalse();
        result.Kind.Should().Be(PlanTransitionKind.SamePlan);
    }

    [Theory]
    [InlineData("Monthly", "Yearly", true)]
    [InlineData("Yearly", "Monthly", true)]
    [InlineData("Monthly", "Monthly", false)]
    public void RequiresCheckoutForCycleChange_Works(string current, string target, bool expected)
    {
        PlanTransitionValidator.RequiresCheckoutForCycleChange(current, target).Should().Be(expected);
    }
}
