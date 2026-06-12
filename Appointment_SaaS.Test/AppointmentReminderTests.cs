using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Core.Constants;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Services;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Context;
using AutoMapper;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Appointment_SaaS.Test;

public class AppointmentReminderTests
{
    private readonly AppDbContext _db;
    private readonly AppointmentManager _manager;

    public AppointmentReminderTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(dbOptions);

        var sector = new Sector { SectorID = 1, Name = "Test", DefaultPrompt = "x", CreatedAt = DateTime.UtcNow };
        _db.Sectors.Add(sector);

        var proTenant = CreateTenant(1, "Pro Salon", "pro", "pro-instance");
        var starterTenant = CreateTenant(2, "Starter Salon", "Starter", "starter-instance");
        _db.Tenants.AddRange(proTenant, starterTenant);

        var tomorrow = DateTime.Today.AddDays(1).Date.AddHours(14);
        _db.Appointments.Add(new Appointment
        {
            TenantID = 1,
            AppUserID = 1,
            ServiceID = 1,
            CustomerName = "Ali",
            CustomerPhone = "05317239931",
            StartDate = tomorrow,
            EndDate = tomorrow.AddHours(1),
            Status = "Beklemede",
            Note = "",
            Service = new Service { ServiceID = 1, TenantID = 1, Name = "Kesim", DurationInMinutes = 30, Price = 100 }
        });
        _db.Appointments.Add(new Appointment
        {
            TenantID = 2,
            AppUserID = 1,
            ServiceID = 2,
            CustomerName = "Veli",
            CustomerPhone = "05551234567",
            StartDate = tomorrow,
            EndDate = tomorrow.AddHours(1),
            Status = "Beklemede",
            Note = "",
            Service = new Service { ServiceID = 2, TenantID = 2, Name = "Kesim", DurationInMinutes = 30, Price = 100 }
        });
        _db.SaveChanges();

        var mockAppointmentRepo = new Mock<IAppointmentRepository>();
        var mockTenantRepo = new Mock<ITenantRepository>();
        var mockEvolution = new Mock<IEvolutionApiService>();
        var mockMapper = new Mock<IMapper>();
        var mockTenantProvider = new Mock<ITenantProvider>();
        var mockLogger = new Mock<ILogger<AppointmentManager>>();
        var mockCache = new Mock<IMemoryCache>();
        var mockGoogle = new Mock<IGoogleCalendarService>();

        _manager = new AppointmentManager(
            mockAppointmentRepo.Object,
            mockMapper.Object,
            mockTenantRepo.Object,
            mockEvolution.Object,
            _db,
            mockTenantProvider.Object,
            mockLogger.Object,
            mockCache.Object,
            mockGoogle.Object);
    }

    private static Tenant CreateTenant(int id, string name, string plan, string instance) => new()
    {
        TenantID = id,
        Name = name,
        PlanType = plan,
        InstanceName = instance,
        PhoneNumber = "5550000000",
        Address = "Adres",
        ApiKey = "key",
        SectorID = 1,
        CreatedAt = DateTime.UtcNow,
        IsActive = true,
        IsSubscriptionActive = true,
        IsBlacklisted = false
    };

    [Fact]
    public async Task GetPendingRemindersAsync_ShouldOnlyInclude_ProAndBusinessPlans()
    {
        var pending = await _manager.GetPendingRemindersAsync();

        pending.Should().ContainSingle(x => x.TenantId == 1 && x.CustomerName == "Ali");
        pending.Should().NotContain(x => x.TenantId == 2);
    }

    [Fact]
    public void PlanPricing_CanUseReminders_OnlyProAndBusiness()
    {
        PlanPricing.CanUseReminders("Pro").Should().BeTrue();
        PlanPricing.CanUseReminders("Business").Should().BeTrue();
        PlanPricing.CanUseReminders("Starter").Should().BeFalse();
        PlanPricing.CanUseReminders("Trial").Should().BeFalse();
    }
}
