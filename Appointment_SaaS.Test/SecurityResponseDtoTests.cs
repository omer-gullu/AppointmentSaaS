using System.Text.Json;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using FluentAssertions;
using Xunit;

namespace Appointment_SaaS.Test;

public class SecurityResponseDtoTests
{
    [Fact]
    public void TenantResponseDto_ShouldNotExposeSecrets_InSerializedJson()
    {
        var tenant = new Tenant
        {
            TenantID = 1,
            Name = "Test",
            PhoneNumber = "555",
            Address = "Addr",
            ApiKey = "secret-api-key",
            GoogleAccessToken = "google-secret",
            IyzicoUserKey = "iyz-user",
            IyzicoCardToken = "iyz-card",
            SubscriptionReferenceCode = "checkout-token",
            TrialFingerprint = "fingerprint"
        };

        var json = JsonSerializer.Serialize(TenantResponseDto.FromEntity(tenant));

        json.Should().NotContain("secret-api-key");
        json.Should().NotContain("google-secret");
        json.Should().NotContain("iyz-user");
        json.Should().NotContain("checkout-token");
        json.Should().NotContain("fingerprint");
        json.Should().Contain("HasGoogleConnected");
    }

    [Fact]
    public void StaffListItemDto_ShouldNotExposeOtpOrRefreshToken()
    {
        var user = new AppUser
        {
            AppUserID = 2,
            FirstName = "A",
            LastName = "B",
            Email = "a@b.com",
            PhoneNumber = "555",
            OtpCode = "123456",
            GoogleRefreshToken = "refresh-secret",
            SecurityStamp = "stamp",
            Status = true
        };

        var json = JsonSerializer.Serialize(StaffListItemDto.FromEntity(user));

        json.Should().NotContain("123456");
        json.Should().NotContain("refresh-secret");
        json.Should().NotContain("stamp");
        json.Should().Contain("HasGoogleConnected");
    }
}
