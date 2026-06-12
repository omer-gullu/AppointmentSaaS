using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Core.Constants;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Concrete;
using Appointment_SaaS.Data.Context;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Appointment_SaaS.Test;

public class TenantBlockedPhoneManagerTests
{
    private readonly AppDbContext _db;
    private readonly TenantBlockedPhoneManager _manager;
    private readonly Mock<ITenantService> _mockTenantService;

    public TenantBlockedPhoneManagerTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(dbOptions);

        _db.Tenants.Add(new Tenant
        {
            TenantID = 1,
            Name = "Test Salon",
            PhoneNumber = "5551112233",
            Address = "Test",
            ApiKey = "key",
            SectorID = 1,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            InstanceName = "test-instance"
        });
        _db.SaveChanges();

        _mockTenantService = new Mock<ITenantService>();
        _mockTenantService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(_db.Tenants.First());
        _mockTenantService.Setup(s => s.GetContextByInstanceAsync("test-instance"))
            .ReturnsAsync(_db.Tenants.First());

        var repo = new EfTenantBlockedPhoneRepository(_db);
        var logger = new Mock<ILogger<TenantBlockedPhoneManager>>();
        _manager = new TenantBlockedPhoneManager(repo, _mockTenantService.Object, logger.Object);
    }

    [Fact]
    public async Task AddManualAsync_ShouldNormalizePhone_AndBlockSameCoreInDifferentFormats()
    {
        await _manager.AddManualAsync(1, new TenantBlockedPhoneCreateDto { Phone = "05317239931" });

        var checkJid = await _manager.IsBlockedAsync(1, "905317239931@s.whatsapp.net");
        checkJid.Blocked.Should().BeTrue();
        checkJid.PhoneCore.Should().Be("5317239931");
    }

    [Fact]
    public async Task IsBlockedAsync_ShouldBeFalse_ForOtherTenant()
    {
        await _manager.AddManualAsync(1, new TenantBlockedPhoneCreateDto { Phone = "05317239931" });

        var check = await _manager.IsBlockedAsync(99, "05317239931");
        check.Blocked.Should().BeFalse();
    }

    [Fact]
    public async Task OptOutAsync_ShouldSetSelfOptOutSource()
    {
        var result = await _manager.OptOutAsync(new WhatsAppOptOutDto
        {
            Phone = "+90 531 723 99 31",
            InstanceName = "test-instance",
            CustomerName = "Ali Veli"
        });

        result.Blocked.Should().BeTrue();
        result.PhoneCore.Should().Be("5317239931");

        var list = await _manager.GetByTenantAsync(1);
        list.Should().ContainSingle(x =>
            x.Source == WhatsAppBlockedPhoneSources.SelfOptOut &&
            x.Note == "Asistanı kapat — Ali Veli");
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteOnlyMatchingTenantRow()
    {
        var added = await _manager.AddManualAsync(1, new TenantBlockedPhoneCreateDto { Phone = "05551234567" });
        var removed = await _manager.RemoveAsync(1, added.Id);
        removed.Should().BeTrue();

        var check = await _manager.IsBlockedAsync(1, "05551234567");
        check.Blocked.Should().BeFalse();
    }

    [Fact]
    public async Task AddManualAsync_ShouldThrow_WhenPhoneInvalid()
    {
        var act = () => _manager.AddManualAsync(1, new TenantBlockedPhoneCreateDto { Phone = "123" });
        await act.Should().ThrowAsync<BadHttpRequestException>();
    }
}
