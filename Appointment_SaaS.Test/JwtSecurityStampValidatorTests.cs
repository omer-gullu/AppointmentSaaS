using Appointment_SaaS.API.Authorization;
using Appointment_SaaS.Core.Entities;
using FluentAssertions;
using Xunit;

namespace Appointment_SaaS.Test;

public class JwtSecurityStampValidatorTests
{
    private const string Stamp = "abc-stamp-123";

    [Fact]
    public void IsValid_ShouldReturnTrue_WhenStampMatches()
    {
        var user = new AppUser { AppUserID = 1, SecurityStamp = Stamp };
        JwtSecurityStampValidator.IsValid("1", Stamp, user).Should().BeTrue();
    }

    [Theory]
    [InlineData(null, Stamp)]
    [InlineData("", Stamp)]
    [InlineData("not-a-number", Stamp)]
    [InlineData("1", null)]
    [InlineData("1", "")]
    public void IsValid_ShouldReturnFalse_WhenClaimsInvalid(string? userId, string? stamp)
    {
        var user = new AppUser { AppUserID = 1, SecurityStamp = Stamp };
        JwtSecurityStampValidator.IsValid(userId, stamp, user).Should().BeFalse();
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenUserNull()
    {
        JwtSecurityStampValidator.IsValid("1", Stamp, null).Should().BeFalse();
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenStampMismatch()
    {
        var user = new AppUser { AppUserID = 1, SecurityStamp = "other-stamp" };
        JwtSecurityStampValidator.IsValid("1", Stamp, user).Should().BeFalse();
    }
}
