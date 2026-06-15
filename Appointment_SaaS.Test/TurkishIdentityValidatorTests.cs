using Appointment_SaaS.Core.Utilities;
using Xunit;

namespace Appointment_SaaS.Test;

public class TurkishIdentityValidatorTests
{
    [Theory]
    [InlineData("10000000146")]
    [InlineData("11111111110")]
    public void IsValidTcKimlik_AcceptsKnownValidNumbers(string tc)
    {
        Assert.True(TurkishIdentityValidator.IsValidTcKimlik(tc));
        Assert.True(TurkishIdentityValidator.IsValidTcOrVkn(tc));
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("12345678901")]
    [InlineData("10000000145")]
    [InlineData("1234567890")]
    public void IsValidTcKimlik_RejectsInvalidNumbers(string tc)
    {
        Assert.False(TurkishIdentityValidator.IsValidTcKimlik(tc));
    }

    [Fact]
    public void NormalizeIdentityNumber_StripsNonDigits()
    {
        Assert.Equal("12345678901", TurkishIdentityValidator.NormalizeIdentityNumber("123 456-78901"));
        Assert.Equal("10000000146", TurkishIdentityValidator.NormalizeIdentityNumber(" 10000000146 "));
    }

    [Fact]
    public void IsValidTcOrVkn_RejectsEmpty()
    {
        Assert.False(TurkishIdentityValidator.IsValidTcOrVkn(null));
        Assert.False(TurkishIdentityValidator.IsValidTcOrVkn(""));
    }
}
