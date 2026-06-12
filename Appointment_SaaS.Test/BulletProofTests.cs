using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Services;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Context;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Appointment_SaaS.Test
{
    /// <summary>
    /// AppointmentManager entegrasyon testleri: personel yük dengeleme, Google Event ID.
    /// (Tenant erişim kuralları: TenantAccessEvaluatorTests; webhook path: WebhookProtectedPathEvaluatorTests.)
    /// </summary>
    public class BulletProofTests
    {
        private readonly AppDbContext _db;
        private readonly AppointmentManager _appointmentManager;

        public BulletProofTests()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _db = new AppDbContext(dbOptions);

            var mockAppointmentRepo = new Mock<IAppointmentRepository>();
            var mockTenantRepo = new Mock<ITenantRepository>();
            var mockTenantProvider = new Mock<ITenantProvider>();
            mockTenantProvider.Setup(x => x.GetTenantId()).Returns((int?)null);

            _appointmentManager = new AppointmentManager(
                mockAppointmentRepo.Object,
                null!,
                mockTenantRepo.Object,
                null!,
                _db,
                mockTenantProvider.Object,
                new Mock<ILogger<AppointmentManager>>().Object,
                new Mock<Microsoft.Extensions.Caching.Memory.IMemoryCache>().Object,
                new Mock<IGoogleCalendarService>().Object
            );
        }

        [Fact]
        public async Task GetStaffWithFewest_ShouldAssignToIdleStaff_WhenOneHasMoreAppointments()
        {
            var today = DateTime.Today.AddHours(10);
            int tenantId = 100;

            var tenant = new Tenant
            {
                TenantID = tenantId,
                Name = "Test Dükkan",
                PhoneNumber = "5551234567",
                Address = "Test",
                ApiKey = "TEST-KEY",
                TrialFingerprint = "",
                AppUsers = new List<AppUser>
                {
                    new AppUser { AppUserID = 1, TenantID = tenantId, Status = true, FirstName = "Yorgun", LastName = "Personel", PhoneNumber = "1111", Email = "yorgun@test.com" },
                    new AppUser { AppUserID = 2, TenantID = tenantId, Status = true, FirstName = "Dinlenmis", LastName = "Personel", PhoneNumber = "2222", Email = "dinlenmis@test.com" }
                }
            };
            await _db.Tenants.AddAsync(tenant);

            for (int i = 0; i < 3; i++)
            {
                await _db.Appointments.AddAsync(new Appointment
                {
                    TenantID = tenantId, AppUserID = 1, ServiceID = 1,
                    CustomerName = $"Müşteri{i}", CustomerPhone = "555",
                    StartDate = today.AddHours(i), EndDate = today.AddHours(i + 1),
                    Status = "Beklemede", Note = ""
                });
            }

            await _db.Appointments.AddAsync(new Appointment
            {
                TenantID = tenantId, AppUserID = 2, ServiceID = 1,
                CustomerName = "MüşteriX", CustomerPhone = "555",
                StartDate = today, EndDate = today.AddHours(1),
                Status = "Beklemede", Note = ""
            });
            await _db.SaveChangesAsync();

            var assignedStaffId = await _appointmentManager.GetStaffWithFewestAppointmentsAsync(tenantId, today);

            assignedStaffId.Should().Be(2);
        }

        [Fact]
        public async Task GetStaffWithFewest_ShouldAssignToFirstById_WhenLoadIsEqual()
        {
            var today = DateTime.Today.AddHours(9);
            int tenantId = 101;

            var tenant = new Tenant
            {
                TenantID = tenantId,
                Name = "Test Dükkan 2",
                PhoneNumber = "5550000000",
                Address = "Test",
                ApiKey = "TEST2",
                TrialFingerprint = "",
                AppUsers = new List<AppUser>
                {
                    new AppUser { AppUserID = 10, TenantID = tenantId, Status = true, FirstName = "A", LastName = "B", PhoneNumber = "1010", Email = "a@test.com" },
                    new AppUser { AppUserID = 11, TenantID = tenantId, Status = true, FirstName = "C", LastName = "D", PhoneNumber = "1111", Email = "c@test.com" }
                }
            };
            await _db.Tenants.AddAsync(tenant);

            foreach (var staffId in new[] { 10, 11 })
            {
                for (int i = 0; i < 2; i++)
                {
                    await _db.Appointments.AddAsync(new Appointment
                    {
                        TenantID = tenantId, AppUserID = staffId, ServiceID = 1,
                        CustomerName = $"M{staffId}-{i}", CustomerPhone = "555",
                        StartDate = today.AddHours(i + staffId), EndDate = today.AddHours(i + staffId + 1),
                        Status = "Beklemede", Note = ""
                    });
                }
            }
            await _db.SaveChangesAsync();

            var assignedStaffId = await _appointmentManager.GetStaffWithFewestAppointmentsAsync(tenantId, today);

            assignedStaffId.Should().Be(10);
        }

        [Fact]
        public async Task GetStaffWithFewest_ShouldReturnNull_WhenNoActiveStaff()
        {
            int tenantId = 102;
            await _db.Tenants.AddAsync(new Tenant
            {
                TenantID = tenantId,
                Name = "Personelsiz Dükkan",
                PhoneNumber = "5550000001",
                Address = "Test",
                ApiKey = "TEST3",
                TrialFingerprint = "",
                AppUsers = new List<AppUser>
                {
                    new AppUser { AppUserID = 20, TenantID = tenantId, Status = false, FirstName = "İzinli", LastName = "Personel", PhoneNumber = "2020", Email = "izinli@test.com" }
                }
            });
            await _db.SaveChangesAsync();

            var result = await _appointmentManager.GetStaffWithFewestAppointmentsAsync(tenantId, DateTime.Today);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetStaffWithFewest_ShouldAssignToOnlyStaff_WhenSingleActive()
        {
            int tenantId = 103;
            await _db.Tenants.AddAsync(new Tenant
            {
                TenantID = tenantId,
                Name = "Tek Kişilik Dükkan",
                PhoneNumber = "5550000002",
                Address = "Test",
                ApiKey = "TEST4",
                TrialFingerprint = "",
                AppUsers = new List<AppUser>
                {
                    new AppUser { AppUserID = 30, TenantID = tenantId, Status = true, FirstName = "Tek", LastName = "Personel", PhoneNumber = "3030", Email = "tek@test.com" }
                }
            });
            await _db.SaveChangesAsync();

            var result = await _appointmentManager.GetStaffWithFewestAppointmentsAsync(tenantId, DateTime.Today);

            result.Should().Be(30);
        }

        [Fact]
        public async Task UpdateGoogleEventIdAsync_ShouldSaveId_WhenAppointmentExists()
        {
            var appointment = new Appointment
            {
                TenantID = 1, AppUserID = 1, ServiceID = 1,
                CustomerName = "Test Müşteri", CustomerPhone = "555",
                StartDate = DateTime.Today.AddHours(10),
                EndDate = DateTime.Today.AddHours(11),
                Status = "Beklemede", Note = "",
                GoogleEventID = null
            };
            await _db.Appointments.AddAsync(appointment);
            await _db.SaveChangesAsync();

            var appointmentId = appointment.AppointmentID;

            var result = await _appointmentManager.UpdateGoogleEventIdAsync(appointmentId, "google_event_abc123");

            result.Should().BeTrue();
            var updated = await _db.Appointments.FindAsync(appointmentId);
            updated!.GoogleEventID.Should().Be("google_event_abc123");
        }

        [Fact]
        public async Task UpdateGoogleEventIdAsync_ShouldReturnFalse_WhenAppointmentNotFound()
        {
            var result = await _appointmentManager.UpdateGoogleEventIdAsync(99999, "some_event_id");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateGoogleEventIdAsync_ShouldOverwrite_WhenCalledTwice()
        {
            var appointment = new Appointment
            {
                TenantID = 1, AppUserID = 1, ServiceID = 1,
                CustomerName = "Retry Müşteri", CustomerPhone = "555",
                StartDate = DateTime.Today.AddHours(14),
                EndDate = DateTime.Today.AddHours(15),
                Status = "Beklemede", Note = "",
                GoogleEventID = "eski_event_id"
            };
            await _db.Appointments.AddAsync(appointment);
            await _db.SaveChangesAsync();

            await _appointmentManager.UpdateGoogleEventIdAsync(appointment.AppointmentID, "yeni_event_id");

            var updated = await _db.Appointments.FindAsync(appointment.AppointmentID);
            updated!.GoogleEventID.Should().Be("yeni_event_id");
        }
    }
}
