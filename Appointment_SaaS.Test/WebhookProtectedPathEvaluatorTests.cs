using Appointment_SaaS.API.Authorization;
using FluentAssertions;
using Xunit;

namespace Appointment_SaaS.Test;

public class WebhookProtectedPathEvaluatorTests
{
    [Theory]
    [InlineData("/api/appointments/available-slots", "GET")]
    [InlineData("/api/services/businessphone/905551234567", "GET")]
    [InlineData("/api/services/tenant/3", "GET")]
    [InlineData("/api/services/42", "GET")]
    [InlineData("/api/appointments/customer/5317239931", "GET")]
    [InlineData("/api/tenants/getcontextbyinstance", "GET")]
    [InlineData("/api/appointments", "POST")]
    [InlineData("/api/feedbacks", "POST")]
    [InlineData("/api/whatsappblockedphones/check", "GET")]
    [InlineData("/api/whatsappblockedphones/opt-out", "POST")]
    [InlineData("/api/appointments/reminders/pending", "GET")]
    [InlineData("/api/appointments/reminders/run", "POST")]
    [InlineData("/api/appointments/lock", "POST")]
    [InlineData("/api/appointments/my-active-appointments", "GET")]
    [InlineData("/api/appointments/123", "DELETE")]
    [InlineData("/api/appointments/123", "PUT")]
    public void RequiresWebhookToken_ShouldBeTrue_ForN8nAndSensitivePaths(string path, string method)
    {
        WebhookProtectedPathEvaluator.RequiresWebhookToken(path, method).Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/iyzico/webhook", "POST")]
    [InlineData("/api/sector", "GET")]
    [InlineData("/api/auth/login", "POST")]
    [InlineData("/api/appointments/abc", "DELETE")]
    [InlineData("/api/appointments/abc", "PUT")]
    public void RequiresWebhookToken_ShouldBeFalse_ForPublicOrSelfSecuredPaths(string path, string method)
    {
        WebhookProtectedPathEvaluator.RequiresWebhookToken(path, method).Should().BeFalse();
    }

    [Fact]
    public void IsExcluded_IyzicoWebhook()
    {
        WebhookProtectedPathEvaluator.IsExcluded("/api/iyzico/webhook").Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/appointments/reminders/run", "POST", true)]
    [InlineData("/api/appointments/reminders/pending", "GET", true)]
    [InlineData("/api/appointments/customer/555", "GET", false)]
    public void IsSystemOnlyPath_ShouldMatchCronEndpoints(string path, string method, bool expected)
    {
        WebhookProtectedPathEvaluator.IsSystemOnlyPath(path, method).Should().Be(expected);
    }
}
