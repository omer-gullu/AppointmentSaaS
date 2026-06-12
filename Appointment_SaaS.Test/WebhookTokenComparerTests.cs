using Appointment_SaaS.API.Authorization;
using FluentAssertions;
using Xunit;

namespace Appointment_SaaS.Test;

public class WebhookTokenComparerTests
{
    private const string Expected = "super-secret-token";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("yanlis-token")]
    public void Matches_ShouldReturnFalse_WhenTokenInvalid(string? provided)
    {
        WebhookTokenComparer.Matches(provided, Expected).Should().BeFalse();
    }

    [Fact]
    public void Matches_ShouldReturnTrue_WhenTokenCorrect()
    {
        WebhookTokenComparer.Matches(Expected, Expected).Should().BeTrue();
    }

    [Fact]
    public void Matches_ShouldReturnFalse_WhenExpectedEmpty()
    {
        WebhookTokenComparer.Matches(Expected, "").Should().BeFalse();
    }
}
