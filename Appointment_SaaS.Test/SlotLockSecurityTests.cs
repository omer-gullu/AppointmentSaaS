using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Core.Services;
using Appointment_SaaS.Core.Utilities;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Context;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Appointment_SaaS.Test;

public class SlotLockSecurityTests
{
    [Theory]
    [InlineData("slot_lock:5:202605011400", 5, true)]
    [InlineData("slot_lock:5:202605011400", 3, false)]
    [InlineData("invalid", 5, false)]
    public void SlotLockKeyParser_TryGetTenantId(string key, int expected, bool shouldMatch)
    {
        var ok = SlotLockKeyParser.TryGetTenantId(key, out var tenantId);
        Assert.Equal(shouldMatch, ok && tenantId == expected);
    }

    [Fact]
    public void ReleaseSlotLock_RejectsCrossTenant()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new AppDbContext(dbOptions);

        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(x => x.GetTenantId()).Returns((int?)null);

        var manager = new AppointmentManager(
            Mock.Of<IAppointmentRepository>(),
            Mock.Of<IMapper>(),
            Mock.Of<ITenantRepository>(),
            Mock.Of<IEvolutionApiService>(),
            db,
            mockTenantProvider.Object,
            NullLogger<AppointmentManager>.Instance,
            cache,
            Mock.Of<IGoogleCalendarService>());

        manager.TryAcquireSlotLock(5, new DateTime(2026, 5, 1, 14, 0, 0), out var lockKey);

        Assert.Throws<UnauthorizedAccessException>(() =>
            manager.ReleaseSlotLock(lockKey, expectedTenantId: 99));

        manager.ReleaseSlotLock(lockKey, expectedTenantId: 5);
    }
}
