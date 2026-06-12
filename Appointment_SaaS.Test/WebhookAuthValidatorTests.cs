using Appointment_SaaS.API.Authorization;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Appointment_SaaS.Test;

public class WebhookAuthValidatorTests
{
    private static WebhookAuthValidator CreateValidator(
        string systemToken,
        Tenant? tenantForInstance = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebhookSecurity:N8nAuthToken"] = systemToken
            })
            .Build();

        var mockTenant = new Mock<ITenantService>();
        mockTenant
            .Setup(s => s.GetContextByInstanceAsync("shop-a"))
            .ReturnsAsync(tenantForInstance);
        mockTenant
            .Setup(s => s.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => tenantForInstance?.TenantID == id ? tenantForInstance : null);

        return new WebhookAuthValidator(config, mockTenant.Object);
    }

    private static DefaultHttpContext CreateContext(string path, string method, string? token, int? tenantId = null, string? instanceName = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Request.Method = method;
        if (!string.IsNullOrEmpty(token))
            ctx.Request.Headers["X-Auth-Token"] = token;
        if (tenantId.HasValue)
            ctx.Request.Headers["X-Tenant-Id"] = tenantId.Value.ToString();
        if (!string.IsNullOrEmpty(instanceName))
            ctx.Request.QueryString = new QueryString($"?instanceName={instanceName}");
        return ctx;
    }

    [Fact]
    public async Task SystemPath_ShouldAllow_OnlyGlobalToken()
    {
        var tenant = new Tenant { TenantID = 1, ApiKey = "TENANT-KEY", IsActive = true };
        var validator = CreateValidator("SYSTEM-TOKEN", tenant);
        var ctx = CreateContext("/api/appointments/reminders/run", "POST", "SYSTEM-TOKEN");

        var result = await validator.EvaluateAsync(ctx, ctx.Request.Path, ctx.Request.Method);

        result.Kind.Should().Be(WebhookAuthResultKind.AllowSystem);
    }

    [Fact]
    public async Task SystemPath_ShouldReject_TenantApiKey()
    {
        var tenant = new Tenant { TenantID = 1, ApiKey = "TENANT-KEY", IsActive = true };
        var validator = CreateValidator("SYSTEM-TOKEN", tenant);
        var ctx = CreateContext("/api/appointments/reminders/run", "POST", "TENANT-KEY");

        var result = await validator.EvaluateAsync(ctx, ctx.Request.Path, ctx.Request.Method);

        result.Kind.Should().Be(WebhookAuthResultKind.Unauthorized);
    }

    [Fact]
    public async Task TenantPath_ShouldAllow_TenantApiKey_WithTenantHeader()
    {
        var tenant = new Tenant { TenantID = 5, ApiKey = "TENANT-KEY", IsActive = true };
        var validator = CreateValidator("SYSTEM-TOKEN", tenant);
        var ctx = CreateContext("/api/appointments/customer/555", "GET", "TENANT-KEY", tenantId: 5);

        var result = await validator.EvaluateAsync(ctx, ctx.Request.Path, ctx.Request.Method);

        result.Kind.Should().Be(WebhookAuthResultKind.AllowTenant);
        result.TenantId.Should().Be(5);
    }

    [Fact]
    public async Task TenantPath_ShouldReject_GlobalToken_WithoutTenantScope()
    {
        var tenant = new Tenant { TenantID = 5, ApiKey = "TENANT-KEY", IsActive = true };
        var validator = CreateValidator("SYSTEM-TOKEN", tenant);
        var ctx = CreateContext("/api/appointments/customer/555", "GET", "SYSTEM-TOKEN", tenantId: 5);

        var result = await validator.EvaluateAsync(ctx, ctx.Request.Path, ctx.Request.Method);

        result.Kind.Should().Be(WebhookAuthResultKind.Unauthorized);
    }

    [Fact]
    public async Task TenantPath_ShouldResolveTenant_FromInstanceName()
    {
        var tenant = new Tenant { TenantID = 3, ApiKey = "SHOP-KEY", InstanceName = "shop-a", IsActive = true };
        var validator = CreateValidator("SYSTEM-TOKEN", tenant);
        var ctx = CreateContext("/api/tenants/getcontextbyinstance", "GET", "SHOP-KEY", instanceName: "shop-a");

        var result = await validator.EvaluateAsync(ctx, ctx.Request.Path, ctx.Request.Method);

        result.Kind.Should().Be(WebhookAuthResultKind.AllowTenant);
        result.TenantId.Should().Be(3);
    }

    [Fact]
    public async Task BootstrapPath_ShouldAllow_SystemToken_AndResolveTenant()
    {
        var tenant = new Tenant { TenantID = 3, ApiKey = "SHOP-KEY", InstanceName = "shop-a", IsActive = true };
        var validator = CreateValidator("SYSTEM-TOKEN", tenant);
        var ctx = CreateContext("/api/tenants/getcontextbyinstance", "GET", "SYSTEM-TOKEN", instanceName: "shop-a");

        var result = await validator.EvaluateAsync(ctx, ctx.Request.Path, ctx.Request.Method);

        result.Kind.Should().Be(WebhookAuthResultKind.AllowTenant);
        result.TenantId.Should().Be(3);
    }

    [Fact]
    public async Task TenantPath_OtherThanBootstrap_ShouldStillReject_SystemToken()
    {
        var tenant = new Tenant { TenantID = 5, ApiKey = "TENANT-KEY", IsActive = true };
        var validator = CreateValidator("SYSTEM-TOKEN", tenant);
        var ctx = CreateContext("/api/appointments/customer/555", "GET", "SYSTEM-TOKEN", tenantId: 5);

        var result = await validator.EvaluateAsync(ctx, ctx.Request.Path, ctx.Request.Method);

        result.Kind.Should().Be(WebhookAuthResultKind.Unauthorized);
    }
}
