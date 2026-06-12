using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Services;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Context;
using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using Xunit;
using Appointment_SaaS.Test.TestHelpers;

namespace Appointment_SaaS.Test
{
    /// <summary>
    /// Dış Servis Çökme (Resilience) Testleri:
    /// WhatsApp, Google Calendar veya her ikisi birden çöktüğünde
    /// çekirdek işlemlerin (randevu CRUD) kesintisiz çalıştığını doğrular.
    /// </summary>
    public class ResilienceTests
    {
        private readonly Mock<IAppointmentRepository> _mockAppointmentRepo;
        private readonly Mock<ITenantRepository> _mockTenantRepo;
        private readonly Mock<IEvolutionApiService> _mockEvolutionService;
        private readonly Mock<IGoogleCalendarService> _mockGoogleService;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ILogger<AppointmentManager>> _mockLogger;
        private readonly AppDbContext _db;
        private readonly int _staffId;
        private readonly AppointmentManager _manager;

        public ResilienceTests()
        {
            _mockAppointmentRepo = new Mock<IAppointmentRepository>();
            _mockTenantRepo = new Mock<ITenantRepository>();
            _mockEvolutionService = new Mock<IEvolutionApiService>();
            _mockGoogleService = new Mock<IGoogleCalendarService>();
            _mockMapper = new Mock<IMapper>();
            _mockLogger = new Mock<ILogger<AppointmentManager>>();

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _db = new AppDbContext(dbOptions);

            var mockTenantProvider = new Mock<ITenantProvider>();
            mockTenantProvider.Setup(x => x.GetTenantId()).Returns((int?)null);

            var mockCache = new Mock<IMemoryCache>();

            _manager = new AppointmentManager(
                _mockAppointmentRepo.Object,
                _mockMapper.Object,
                _mockTenantRepo.Object,
                _mockEvolutionService.Object,
                _db,
                mockTenantProvider.Object,
                _mockLogger.Object,
                mockCache.Object,
                _mockGoogleService.Object
            );

            _staffId = AppointmentTestSeeds.EnsureStaffWithGoogleAsync(_db).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Ortak test verisi oluşturur: Tenant + BusinessHour + AppointmentCreateDto
        /// </summary>
        private (AppointmentCreateDto dto, Tenant tenant) CreateTestData()
        {
            var dto = new AppointmentCreateDto
            {
                TenantID = 1,
                AppUserID = _staffId,
                ServiceID = 1,
                StartDate = DateTime.Now.AddDays(1).Date.AddHours(10),
                EndDate = DateTime.Now.AddDays(1).Date.AddHours(11),
                CustomerName = "Test Müşteri",
                CustomerPhone = "5551112233",
                Note = "Resilience Test"
            };

            var tenant = new Tenant
            {
                TenantID = 1,
                InstanceName = "test_instance",
                BusinessHours = new List<BusinessHour>
                {
                    new BusinessHour
                    {
                        DayOfWeek = (int)dto.StartDate.DayOfWeek,
                        IsClosed = false,
                        OpenTime = TimeSpan.FromHours(9),
                        CloseTime = TimeSpan.FromHours(18)
                    }
                }
            };

            // Tenant repo mock
            _mockTenantRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .Returns((Expression<Func<Tenant, bool>> predicate) =>
                    new List<Tenant> { tenant }.AsQueryable().Where(predicate).BuildMock());

            // Boş randevu listesi (çakışma yok)
            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                .Returns((Expression<Func<Appointment, bool>> predicate) =>
                    new List<Appointment>().AsQueryable().Where(predicate).BuildMock());

            // Mapper mock
            var fakeAppointment = new Appointment
            {
                AppointmentID = 1,
                TenantID = dto.TenantID,
                AppUserID = dto.AppUserID ?? 0,
                ServiceID = dto.ServiceID,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                CustomerName = dto.CustomerName,
                CustomerPhone = dto.CustomerPhone
            };
            _mockMapper.Setup(m => m.Map<Appointment>(dto)).Returns(fakeAppointment);

            return (dto, tenant);
        }

        public enum ExternalFailureMode { WhatsAppOnly, GoogleOnly, Both }

        [Theory]
        [InlineData(ExternalFailureMode.WhatsAppOnly)]
        [InlineData(ExternalFailureMode.GoogleOnly)]
        [InlineData(ExternalFailureMode.Both)]
        public async Task AddAppointment_ShouldPersistCoreData_WhenExternalServicesFail(ExternalFailureMode mode)
        {
            var (dto, _) = CreateTestData();
            Appointment? saved = null;
            _mockAppointmentRepo.Setup(x => x.AddAsync(It.IsAny<Appointment>()))
                .Callback<Appointment>(a => { a.AppointmentID = 1; saved = a; })
                .Returns(Task.CompletedTask);
            _mockAppointmentRepo.Setup(x => x.SaveAsync()).ReturnsAsync(1);

            if (mode is ExternalFailureMode.WhatsAppOnly)
            {
                _mockEvolutionService
                    .Setup(x => x.SendWhatsAppMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ThrowsAsync(new HttpRequestException("WhatsApp unavailable"));
                _mockGoogleService
                    .Setup(x => x.AddEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                    .ReturnsAsync("google_event_123");

                var appointmentId = await _manager.AddAppointmentAsync(dto);

                appointmentId.Should().Be(1);
                saved.Should().NotBeNull();
                saved!.TenantID.Should().Be(dto.TenantID);
                saved.CustomerPhone.Should().Be(dto.CustomerPhone);
                saved.CustomerName.Should().Be(dto.CustomerName);
                saved.Status.Should().Be("Beklemede");
                _mockAppointmentRepo.Verify(x => x.AddAsync(It.IsAny<Appointment>()), Times.Once);
                return;
            }

            _mockEvolutionService
                .Setup(x => x.SendWhatsAppMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockGoogleService
                .Setup(x => x.AddEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new Exception("Google API failure"));

            Func<Task> act = async () => await _manager.AddAppointmentAsync(dto);
            await act.Should().ThrowAsync<BadHttpRequestException>()
                .WithMessage("*Google Takvime yazılamadı*");
            _mockAppointmentRepo.Verify(x => x.AddAsync(It.IsAny<Appointment>()), Times.Once);
        }

        // ═══════════════════════════════════════════════════════
        //  4. GÜNCELLEME — GOOGLE CALENDAR ÇÖKTÜĞÜNDE DB GÜNCELLENMELİ
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task UpdateAppointment_ShouldSucceed_WhenGoogleCalendarThrowsException()
        {
            // Arrange: DB'de mevcut randevu
            var appointment = new Appointment
            {
                AppointmentID = 10,
                TenantID = 1,
                AppUserID = _staffId,
                ServiceID = 1,
                CustomerName = "Mevcut Müşteri",
                CustomerPhone = "5559998877",
                StartDate = DateTime.Now.AddDays(1).Date.AddHours(14),
                EndDate = DateTime.Now.AddDays(1).Date.AddHours(15),
                Status = "Beklemede",
                Note = "Test",
                GoogleEventID = "existing_google_event"
            };

            var tenant = new Tenant
            {
                TenantID = 1,
                BusinessHours = new List<BusinessHour>
                {
                    new BusinessHour
                    {
                        DayOfWeek = (int)appointment.StartDate.DayOfWeek,
                        IsClosed = false,
                        OpenTime = TimeSpan.FromHours(9),
                        CloseTime = TimeSpan.FromHours(18)
                    }
                }
            };

            _mockTenantRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .Returns((Expression<Func<Tenant, bool>> predicate) =>
                    new List<Tenant> { tenant }.AsQueryable().Where(predicate).BuildMock());

            // Slot conflict check için boş liste (çakışma yok)
            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                .Returns((Expression<Func<Appointment, bool>> predicate) =>
                    new List<Appointment>().AsQueryable().Where(predicate).BuildMock());

            // Google Calendar güncelleme çöksün
            _mockGoogleService
                .Setup(x => x.UpdateEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new Exception("Google API: Rate limit exceeded"));

            // Act: Randevu saatini değiştir
            appointment.StartDate = appointment.StartDate.AddHours(1);
            appointment.EndDate = appointment.EndDate.AddHours(1);

            Func<Task> act = async () => await _manager.UpdateAsync(appointment);

            await act.Should().NotThrowAsync();
            appointment.StartDate.Hour.Should().Be(15);
            _mockAppointmentRepo.Verify(x => x.Update(appointment), Times.Once);
            _mockAppointmentRepo.Verify(x => x.SaveAsync(), Times.Once);
        }

        // ═══════════════════════════════════════════════════════
        //  5. SİLME — GOOGLE CALENDAR ÇÖKTÜĞÜNDE DB'DEN SİLİNMELİ
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task DeleteAppointment_ShouldSucceed_WhenGoogleCalendarThrowsException()
        {
            // Arrange: Silinecek randevu — Google event ID'si var
            var appointment = new Appointment
            {
                AppointmentID = 20,
                TenantID = 1,
                AppUserID = _staffId,
                ServiceID = 1,
                CustomerName = "Silinecek Müşteri",
                CustomerPhone = "5550001122",
                StartDate = DateTime.Now.AddDays(2).Date.AddHours(10),
                EndDate = DateTime.Now.AddDays(2).Date.AddHours(11),
                Status = "Beklemede",
                Note = "",
                GoogleEventID = "google_event_to_delete"
            };

            // Google Calendar silme çöksün
            _mockGoogleService
                .Setup(x => x.DeleteEventAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Google API: 404 Event not found"));

            // Act
            Func<Task> act = async () => await _manager.DeleteAsync(appointment);

            // Assert: Google çökse bile randevu DB'den silindi
            await act.Should().NotThrowAsync();
            _mockAppointmentRepo.Verify(x => x.Delete(appointment), Times.Once);
            _mockAppointmentRepo.Verify(x => x.SaveAsync(), Times.Once);
        }

        // ═══════════════════════════════════════════════════════
        //  6. PERSONEL DEĞİŞİKLİĞİ — GOOGLE ÇÖKTÜĞÜNDE DB GÜNCELLENMELİ
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task UpdateAppointment_ShouldSucceed_WhenStaffChangedAndGoogleFails()
        {
            // Arrange: Personel 1'den Personel 2'ye aktarılacak randevu
            var appointment = new Appointment
            {
                AppointmentID = 30,
                TenantID = 1,
                AppUserID = 2, // Yeni personel
                ServiceID = 1,
                CustomerName = "Transfer Müşteri",
                CustomerPhone = "5553334455",
                StartDate = DateTime.Now.AddDays(1).Date.AddHours(11),
                EndDate = DateTime.Now.AddDays(1).Date.AddHours(12),
                Status = "Beklemede",
                Note = "",
                GoogleEventID = "old_staff_event"
            };

            var tenant = new Tenant
            {
                TenantID = 1,
                BusinessHours = new List<BusinessHour>
                {
                    new BusinessHour
                    {
                        DayOfWeek = (int)appointment.StartDate.DayOfWeek,
                        IsClosed = false,
                        OpenTime = TimeSpan.FromHours(9),
                        CloseTime = TimeSpan.FromHours(18)
                    }
                }
            };

            _mockTenantRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .Returns((Expression<Func<Tenant, bool>> predicate) =>
                    new List<Tenant> { tenant }.AsQueryable().Where(predicate).BuildMock());

            // Slot conflict check için boş liste (çakışma yok)
            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                .Returns((Expression<Func<Appointment, bool>> predicate) =>
                    new List<Appointment>().AsQueryable().Where(predicate).BuildMock());

            // Yeni personelin takvimine ekleme başarısız → eski event silinmemeli
            _mockGoogleService
                .Setup(x => x.AddEventAsync(2, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync((string?)null);

            // Act: previousAppUserID = 1 (eski personel)
            Func<Task> act = async () => await _manager.UpdateAsync(appointment, previousAppUserID: 1);

            await act.Should().NotThrowAsync();
            appointment.AppUserID.Should().Be(2);
            appointment.GoogleEventID.Should().Be("old_staff_event");
            _mockGoogleService.Verify(x => x.DeleteEventAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            _mockAppointmentRepo.Verify(x => x.Update(appointment), Times.Once);
            _mockAppointmentRepo.Verify(x => x.SaveAsync(), Times.Once);
        }

        // ═══════════════════════════════════════════════════════
        //  7. WHATSAPP ÇÖKTÜĞÜNDE HATA LOGLANDI MI?
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task AddAppointment_ShouldLogWarning_WhenWhatsAppFails()
        {
            // Arrange
            var (dto, _) = CreateTestData();

            _mockEvolutionService
                .Setup(x => x.SendWhatsAppMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new HttpRequestException("Connection refused"));

            _mockGoogleService
                .Setup(x => x.AddEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync("google_event_123");

            // Act
            await _manager.AddAppointmentAsync(dto);

            // Assert: Logger.LogWarning çağrıldı mı?
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("WhatsApp bildirimi gönderilemedi")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once,
                "WhatsApp hatası loglanmalıdır.");
        }

        // ═══════════════════════════════════════════════════════
        //  8. GOOGLE CALENDAR ÇÖKTÜĞÜNDE HATA LOGLANDI MI?
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task AddAppointment_ShouldThrowAndLogError_WhenGoogleCalendarFails()
        {
            var (dto, _) = CreateTestData();

            _mockEvolutionService
                .Setup(x => x.SendWhatsAppMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockGoogleService
                .Setup(x => x.AddEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new Exception("Google API: Quota exceeded"));

            Func<Task> act = async () => await _manager.AddAppointmentAsync(dto);
            await act.Should().ThrowAsync<BadHttpRequestException>()
                .WithMessage("*Google Takvime yazılamadı*");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("GoogleCalendar")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
