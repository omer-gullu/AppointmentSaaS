using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Context;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Appointment_SaaS.Test;

/// <summary>
/// İyzico webhook sonrası plan/abonelik durumu — yanlış implementasyonda kırılır.
/// </summary>
public class TenantPlanManagerTests
{
    private readonly Mock<ITenantService> _mockTenantService = new();
    private readonly Mock<IIyzicoPaymentService> _mockIyzico = new();
    private readonly AppDbContext _db;
    private readonly TenantPlanManager _sut;

    public TenantPlanManagerTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(dbOptions);

        _mockTenantService
            .Setup(s => s.UpdateSubscriptionStatusAsync(It.IsAny<Tenant>(), It.IsAny<bool>()))
            .Callback<Tenant, bool>((t, active) =>
            {
                t.IsActive = active;
                t.IsSubscriptionActive = active;
            })
            .Returns(Task.CompletedTask);

        _mockTenantService
            .Setup(s => s.UpdateAsync(It.IsAny<Tenant>()))
            .Returns(Task.CompletedTask);

        _sut = new TenantPlanManager(
            _mockTenantService.Object,
            _mockIyzico.Object,
            _db,
            Mock.Of<ILogger<TenantPlanManager>>());
    }

    [Fact]
    public async Task ApplySubscriptionFromWebhook_WhenFailureWithPendingPlan_ClearsPendingAndRestoresRef_WithoutSuspending()
    {
        var tenant = new Tenant
        {
            TenantID = 42,
            IsActive = true,
            IsSubscriptionActive = true,
            PlanType = "Starter",
            BillingCycle = "Monthly",
            SubscriptionReferenceCode = "NEW-CHECKOUT-REF",
            PreviousSubscriptionReferenceCode = "OLD-LIVE-REF",
            PendingPlanType = "Pro",
            PendingBillingCycle = "Monthly",
            PendingCheckoutToken = "checkout-token",
            PendingPlanEffectiveDate = DateTime.Now.AddDays(5)
        };

        await _sut.ApplySubscriptionFromWebhookAsync(
            tenant, paymentId: "pay-fail-1", rawBody: "{}", isSuccess: false, isFailure: true, isUpgrade: false);

        tenant.PendingPlanType.Should().BeNull();
        tenant.PendingBillingCycle.Should().BeNull();
        tenant.PendingCheckoutToken.Should().BeNull();
        tenant.PendingPlanEffectiveDate.Should().BeNull();
        tenant.PreviousSubscriptionReferenceCode.Should().BeNull();
        tenant.SubscriptionReferenceCode.Should().Be("OLD-LIVE-REF");
        tenant.IsActive.Should().BeTrue();
        tenant.IsSubscriptionActive.Should().BeTrue();

        _mockTenantService.Verify(s => s.UpdateAsync(tenant), Times.Once);
        _mockTenantService.Verify(
            s => s.UpdateSubscriptionStatusAsync(It.IsAny<Tenant>(), false),
            Times.Never);
    }

    [Fact]
    public async Task ApplySubscriptionFromWebhook_WhenFailureWithoutPendingPlan_SuspendsTenant()
    {
        var tenant = new Tenant
        {
            TenantID = 7,
            IsActive = true,
            IsSubscriptionActive = true,
            PlanType = "Pro",
            BillingCycle = "Monthly",
            SubscriptionReferenceCode = "LIVE-REF"
        };

        await _sut.ApplySubscriptionFromWebhookAsync(
            tenant, paymentId: "pay-fail-2", rawBody: "{}", isSuccess: false, isFailure: true, isUpgrade: false);

        tenant.IsActive.Should().BeFalse();
        tenant.IsSubscriptionActive.Should().BeFalse();

        _mockTenantService.Verify(s => s.UpdateSubscriptionStatusAsync(tenant, false), Times.Once);
        _mockTenantService.Verify(s => s.UpdateAsync(tenant), Times.Once);
    }

    [Fact]
    public async Task ApplySubscriptionFromWebhook_WhenSuccess_RefreshesEndDateAndActivates()
    {
        var endDate = new DateTime(2026, 8, 15, 23, 59, 0);
        var tenant = new Tenant
        {
            TenantID = 9,
            IsActive = false,
            IsSubscriptionActive = false,
            PlanType = "Pro",
            BillingCycle = "Monthly",
            SubscriptionReferenceCode = "LIVE-REF",
            PendingCheckoutToken = "stale-checkout",
            SubscriptionEndDate = DateTime.Now.AddDays(-1),
            CancelAtPeriodEnd = true
        };

        _mockIyzico
            .Setup(i => i.GetSubscriptionDetailAsync("LIVE-REF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IyzicoSubscriptionDetailResult(
                "LIVE-REF",
                "ACTIVE",
                endDate,
                "pricing-ref-pro-monthly"));

        await _sut.ApplySubscriptionFromWebhookAsync(
            tenant, paymentId: "pay-ok-1", rawBody: "{}", isSuccess: true, isFailure: false, isUpgrade: false);

        tenant.IsActive.Should().BeTrue();
        tenant.IsSubscriptionActive.Should().BeTrue();
        tenant.PendingCheckoutToken.Should().BeNull();
        tenant.CancelAtPeriodEnd.Should().BeFalse();
        tenant.SubscriptionEndDate.Should().Be(endDate);

        _mockTenantService.Verify(s => s.UpdateSubscriptionStatusAsync(tenant, true), Times.Once);
        _mockTenantService.Verify(s => s.UpdateAsync(tenant), Times.Once);
    }

    [Fact]
    public async Task ApplySubscriptionFromWebhook_WhenNeitherSuccessNorFailure_DoesNothing()
    {
        var tenant = new Tenant
        {
            TenantID = 3,
            IsActive = true,
            IsSubscriptionActive = true,
            PendingCheckoutToken = "keep-me",
            SubscriptionReferenceCode = "REF"
        };

        await _sut.ApplySubscriptionFromWebhookAsync(
            tenant, paymentId: null, rawBody: "{}", isSuccess: false, isFailure: false, isUpgrade: false);

        tenant.PendingCheckoutToken.Should().Be("keep-me");
        _mockTenantService.Verify(s => s.UpdateAsync(It.IsAny<Tenant>()), Times.Never);
        _mockTenantService.Verify(
            s => s.UpdateSubscriptionStatusAsync(It.IsAny<Tenant>(), It.IsAny<bool>()),
            Times.Never);
        _mockIyzico.Verify(
            i => i.GetSubscriptionDetailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
