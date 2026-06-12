using Appointment_SaaS.API.Controller;
using FluentAssertions;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Appointment_SaaS.Test;

public class IyzicoWebhookSignatureTests
{
    [Fact]
    public void VerifyHmacSignature_ValidHexSignature_ReturnsTrue()
    {
        const string body = "{\"eventType\":\"payment.success\"}";
        const string secret = "test-webhook-secret";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hex = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        IyzicoWebhookController.VerifyHmacSignature(body, hex, secret).Should().BeTrue();
    }

    [Fact]
    public void VerifyHmacSignature_InvalidSignature_ReturnsFalse()
    {
        IyzicoWebhookController.VerifyHmacSignature("{}", "bad", "secret").Should().BeFalse();
    }
}
