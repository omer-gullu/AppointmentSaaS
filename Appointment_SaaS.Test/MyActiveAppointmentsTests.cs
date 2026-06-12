using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
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

public class MyActiveAppointmentsTests
{
    private readonly Mock<IAppointmentRepository> _mockAppointmentRepo;
    private readonly AppointmentManager _manager;
    private readonly AppDbContext _db;

    public MyActiveAppointmentsTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(dbOptions);

        _mockAppointmentRepo = new Mock<IAppointmentRepository>();
        var mockTenantRepo = new Mock<ITenantRepository>();
        var mockEvolution = new Mock<IEvolutionApiService>();
        var mockMapper = new Mock<IMapper>();
        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(x => x.GetTenantId()).Returns((int?)null);
        var mockLogger = new Mock<ILogger<AppointmentManager>>();
        var mockCache = new Mock<IMemoryCache>();
        var mockGoogle = new Mock<IGoogleCalendarService>();

        _manager = new AppointmentManager(
            _mockAppointmentRepo.Object,
            mockMapper.Object,
            mockTenantRepo.Object,
            mockEvolution.Object,
            _db,
            mockTenantProvider.Object,
            mockLogger.Object,
            mockCache.Object,
            mockGoogle.Object);
    }

    private static Appointment BuildAppointment(
        int id,
        int tenantId,
        string customerPhone,
        DateTime start,
        string status = "Beklemede",
        string? firstName = "Ahmet",
        string? lastName = "Yılmaz",
        string serviceName = "Saç Kesim")
    {
        return new Appointment
        {
            AppointmentID = id,
            TenantID = tenantId,
            CustomerName = "Test Müşteri",
            CustomerPhone = customerPhone,
            StartDate = start,
            EndDate = start.AddMinutes(30),
            Status = status,
            Note = "",
            ServiceID = 1,
            AppUserID = 1,
            Service = new Service { ServiceID = 1, TenantID = tenantId, Name = serviceName, DurationInMinutes = 30, Price = 100 },
            AppUser = firstName == null && lastName == null
                ? null!
                : new AppUser
                {
                    AppUserID = 1,
                    TenantID = tenantId,
                    FirstName = firstName ?? string.Empty,
                    LastName = lastName ?? string.Empty,
                    Email = "x@y.z",
                    PhoneNumber = "0000",
                    Status = true
                }
        };
    }

    [Fact]
    public async Task ReturnsEmpty_WhenPhoneOrJidIsBlank()
    {
        var result = await _manager.GetActiveAppointmentsForCustomerAsync(1, "   ");

        result.Should().BeEmpty();
        _mockAppointmentRepo.Verify(
            x => x.GetActiveByPhoneAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task FiltersOutCancelledStatus()
    {
        var future = DateTime.Now.AddDays(1);
        var data = new List<Appointment>
        {
            BuildAppointment(1, 1, "905078283441", future, status: "Beklemede"),
            BuildAppointment(2, 1, "905078283441", future.AddHours(1), status: "İptal Edildi"),
            BuildAppointment(3, 1, "905078283441", future.AddHours(2), status: "Cancelled"),
        };
        _mockAppointmentRepo
            .Setup(x => x.GetActiveByPhoneAsync(1, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>()))
            .ReturnsAsync(data);

        var result = await _manager.GetActiveAppointmentsForCustomerAsync(1, "905078283441");

        result.Should().HaveCount(1);
        result[0].AppointmentId.Should().Be(1);
        result[0].Status.Should().Be("Beklemede");
    }

    [Theory]
    [InlineData("+905078283441")]
    [InlineData("905078283441")]
    [InlineData("05078283441")]
    [InlineData("5078283441")]
    [InlineData("905078283441@s.whatsapp.net")]
    public async Task NormalizesAllPhoneVariantsToSameLookup(string input)
    {
        IReadOnlyCollection<string>? capturedKeys = null;
        _mockAppointmentRepo
            .Setup(x => x.GetActiveByPhoneAsync(1, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>()))
            .Callback<int, IReadOnlyCollection<string>, DateTime>((_, keys, _) => capturedKeys = keys)
            .ReturnsAsync(new List<Appointment>());

        await _manager.GetActiveAppointmentsForCustomerAsync(1, input);

        capturedKeys.Should().NotBeNull();
        capturedKeys!.Should().Contain(new[]
        {
            "5078283441",
            "05078283441",
            "905078283441",
            "+905078283441",
            "905078283441@s.whatsapp.net"
        });
    }

    [Fact]
    public async Task PassesTenantIdAndDateThrough()
    {
        int capturedTenantId = 0;
        DateTime capturedNow = default;
        var before = DateTime.Now.AddSeconds(-2);

        _mockAppointmentRepo
            .Setup(x => x.GetActiveByPhoneAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>()))
            .Callback<int, IReadOnlyCollection<string>, DateTime>((tid, _, now) =>
            {
                capturedTenantId = tid;
                capturedNow = now;
            })
            .ReturnsAsync(new List<Appointment>());

        await _manager.GetActiveAppointmentsForCustomerAsync(42, "905078283441");

        capturedTenantId.Should().Be(42);
        capturedNow.Should().BeOnOrAfter(before);
        capturedNow.Should().BeOnOrBefore(DateTime.Now.AddSeconds(2));
    }

    [Fact]
    public async Task MapsStaffAndServiceNamesCorrectly()
    {
        var future = DateTime.Now.AddDays(1);
        var appointment = BuildAppointment(7, 1, "905078283441", future,
            firstName: "Ayşe", lastName: "Demir", serviceName: "Manikür");
        _mockAppointmentRepo
            .Setup(x => x.GetActiveByPhoneAsync(1, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Appointment> { appointment });

        var result = await _manager.GetActiveAppointmentsForCustomerAsync(1, "905078283441");

        result.Should().HaveCount(1);
        var dto = result[0];
        dto.AppointmentId.Should().Be(7);
        dto.CustomerName.Should().Be("Test Müşteri");
        dto.CustomerPhone.Should().Be("905078283441");
        dto.StartTime.Should().Be(future);
        dto.EndTime.Should().Be(future.AddMinutes(30));
        dto.StaffName.Should().Be("Ayşe Demir");
        dto.ServiceName.Should().Be("Manikür");
        dto.Status.Should().Be("Beklemede");
    }

    [Fact]
    public async Task UsesMultiServiceLinks_WhenPresent()
    {
        var future = DateTime.Now.AddDays(1);
        var appointment = BuildAppointment(8, 1, "905078283441", future, serviceName: "Saç");
        appointment.AppointmentServiceLinks = new List<AppointmentServiceLink>
        {
            new AppointmentServiceLink
            {
                AppointmentID = 8, ServiceID = 1, SortOrder = 0,
                Service = new Service { ServiceID = 1, TenantID = 1, Name = "Saç", DurationInMinutes = 30, Price = 100 }
            },
            new AppointmentServiceLink
            {
                AppointmentID = 8, ServiceID = 2, SortOrder = 1,
                Service = new Service { ServiceID = 2, TenantID = 1, Name = "Sakal", DurationInMinutes = 15, Price = 50 }
            }
        };
        _mockAppointmentRepo
            .Setup(x => x.GetActiveByPhoneAsync(1, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Appointment> { appointment });

        var result = await _manager.GetActiveAppointmentsForCustomerAsync(1, "905078283441");

        result[0].ServiceName.Should().Be("Saç, Sakal");
    }

    [Fact]
    public async Task TenantIsolation_OnlyReturnsRowsRepoEmits()
    {
        // Repo katmanı tenant filtresini SQL'de uyguladığı için manager katmanına yalnızca
        // doğru tenant'a ait satırlar gelir. Burada repo'nun farklı tenant göndermediğini
        // doğruluyoruz: manager, repo'dan dönen listeyi olduğu gibi haritalar.
        var future = DateTime.Now.AddDays(1);
        _mockAppointmentRepo
            .Setup(x => x.GetActiveByPhoneAsync(1, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Appointment>
            {
                BuildAppointment(11, 1, "905078283441", future)
            });
        _mockAppointmentRepo
            .Setup(x => x.GetActiveByPhoneAsync(2, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Appointment>());

        var tenant1 = await _manager.GetActiveAppointmentsForCustomerAsync(1, "905078283441");
        var tenant2 = await _manager.GetActiveAppointmentsForCustomerAsync(2, "905078283441");

        tenant1.Should().HaveCount(1);
        tenant1[0].AppointmentId.Should().Be(11);
        tenant2.Should().BeEmpty();
    }
}
