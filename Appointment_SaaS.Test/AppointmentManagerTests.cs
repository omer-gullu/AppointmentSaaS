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
using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.EntityFrameworkCore;
using MockQueryable.Moq;
using Appointment_SaaS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Appointment_SaaS.Test
{
    public class AppointmentManagerTests
    {
        private readonly Mock<IAppointmentRepository> _mockAppointmentRepo;
        private readonly Mock<ITenantRepository> _mockTenantRepo;
        private readonly Mock<IEvolutionApiService> _mockEvolutionService;
        private readonly Mock<IMapper> _mockMapper;
        private readonly AppDbContext _db;
        private readonly AppointmentManager _manager;

        public AppointmentManagerTests()
        {
            _mockAppointmentRepo = new Mock<IAppointmentRepository>();
            _mockTenantRepo = new Mock<ITenantRepository>();
            _mockEvolutionService = new Mock<IEvolutionApiService>();
            _mockMapper = new Mock<IMapper>();

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _db = new AppDbContext(dbOptions);

            // ITenantProvider: null döner — n8n/webhook benzeri anonim istekleri simüle eder
            var mockTenantProvider = new Mock<ITenantProvider>();
            mockTenantProvider.Setup(x => x.GetTenantId()).Returns((int?)null);

            var mockLogger = new Mock<ILogger<AppointmentManager>>();

            _manager = new AppointmentManager(
                _mockAppointmentRepo.Object,
                _mockMapper.Object,
                _mockTenantRepo.Object,
                _mockEvolutionService.Object,
                _db,
                mockTenantProvider.Object,
                mockLogger.Object
            );
        }

        [Fact]
        public async Task AddAppointmentAsync_ShouldThrowException_WhenSlotIsOccupied()
        {
            // Arrange
            var dto = new AppointmentCreateDto
            {
                TenantID = 1,
                StartDate = DateTime.Now.AddDays(1).Date.AddHours(10), // Yarın 10:00
                EndDate = DateTime.Now.AddDays(1).Date.AddHours(11),   // Yarın 11:00
            };

            // Tenant Repo - Sadece BusinessHour taklidi (çalışma saati kısıtlamasına takılmamak için)
            var tenant = new Tenant
            {
                TenantID = 1,
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
            var tenants = new List<Tenant> { tenant };
            // Include kısımlarında hata fırlatmaması için Setup
            _mockTenantRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                           .Returns((Expression<Func<Tenant, bool>> predicate) => tenants.AsQueryable().Where(predicate).BuildMock());

            // Randevular - Dolu Slot Taklidi
            var appointments = new List<Appointment>
            {
                new Appointment
                {
                    AppointmentID = 10,
                    TenantID = 1, 
                    StartDate = dto.StartDate, 
                    EndDate = dto.EndDate 
                }
            };
            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                                .Returns((Expression<Func<Appointment, bool>> predicate) => appointments.AsQueryable().Where(predicate).BuildMock());

            // Act
            Func<Task> act = async () => await _manager.AddAppointmentAsync(dto);

            // Assert
            await act.Should().ThrowAsync<BadHttpRequestException>()
                     .WithMessage("Seçilen saat dilimi başka bir randevu ile çakışıyor.");

            // AddAsync ya da Evolution API KESİNLİKLE çağırılmamış olmalı
            _mockAppointmentRepo.Verify(x => x.AddAsync(It.IsAny<Appointment>()), Times.Never);
            _mockEvolutionService.Verify(x => x.SendWhatsAppMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AddAppointmentAsync_ShouldCallNotificationService_WhenSuccess()
        {
            // Arrange
            var dto = new AppointmentCreateDto
            {
                TenantID = 1,
                StartDate = DateTime.Now.AddDays(1).Date.AddHours(10),
                EndDate = DateTime.Now.AddDays(1).Date.AddHours(11),
                Note = "Test Randevu",
                CustomerPhone = "MUSTERI_TELEFONU",
                CustomerName = "Ali"
            };

            var tenant = new Tenant
            {
                TenantID = 1,
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
            
            _mockTenantRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                           .Returns((Expression<Func<Tenant, bool>> predicate) => new List<Tenant> { tenant }.AsQueryable().Where(predicate).BuildMock());

            // Boş liste dönerek çakışma olmadığını gösterelim.
            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                                .Returns((Expression<Func<Appointment, bool>> predicate) => new List<Appointment>().AsQueryable().Where(predicate).BuildMock());

            var fakeAppointment = new Appointment 
            { 
                AppointmentID = 99, 
                StartDate = dto.StartDate
            };
            _mockMapper.Setup(m => m.Map<Appointment>(dto)).Returns(fakeAppointment);

            _mockEvolutionService.Setup(x => x.SendWhatsAppMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                 .ReturnsAsync(true); // Succesful send

            // Act
            var result = await _manager.AddAppointmentAsync(dto);

            // Assert
            result.Should().Be(99); 
            _mockAppointmentRepo.Verify(x => x.AddAsync(fakeAppointment), Times.Once);
            _mockAppointmentRepo.Verify(x => x.SaveAsync(), Times.Once);
            _mockEvolutionService.Verify(x => x.SendWhatsAppMessageAsync($"tenant_{dto.TenantID}", "MUSTERI_TELEFONU", It.Is<string>(s => s.Contains("başarıyla oluşturulmuştur"))), Times.Once);
        }

        [Fact]
        public async Task IsSlotAvailableAsync_ShouldReturnFalse_WhenTimeOverlaps()
        {
            // Arrange
            int tenantId = 1;
            var targetStart = new DateTime(2030, 1, 1, 10, 0, 0);
            var targetEnd = new DateTime(2030, 1, 1, 11, 0, 0);

            var existingAppointments = new List<Appointment>
            {
                new Appointment
                {
                    TenantID = tenantId,
                    StartDate = new DateTime(2030, 1, 1, 10, 30, 0), // 10:30-11:30 (Overlap with 10:00-11:00)
                    EndDate = new DateTime(2030, 1, 1, 11, 30, 0)
                }
            };

            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                                .Returns((Expression<Func<Appointment, bool>> predicate) => existingAppointments.AsQueryable().Where(predicate).BuildMock());

            // Act
            var result = await _manager.IsSlotAvailableAsync(tenantId, targetStart, targetEnd);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsSlotAvailableAsync_ShouldReturnTrue_WhenTimeIsFree()
        {
            // Arrange
            int tenantId = 1;
            var targetStart = new DateTime(2030, 1, 1, 10, 0, 0);
            var targetEnd = new DateTime(2030, 1, 1, 11, 0, 0);

            var existingAppointments = new List<Appointment>
            {
                new Appointment
                {
                    TenantID = tenantId,
                    StartDate = new DateTime(2030, 1, 1, 11, 0, 0), // 11:00-12:00 (No Overlap with 10:00-11:00)
                    EndDate = new DateTime(2030, 1, 1, 12, 0, 0)
                }
            };

            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                                .Returns((Expression<Func<Appointment, bool>> predicate) => existingAppointments.AsQueryable().Where(predicate).BuildMock());

            // Act
            var result = await _manager.IsSlotAvailableAsync(tenantId, targetStart, targetEnd);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task GetAllByTenantIdAsync_ShouldOnlyReturnRequestedTenantData()
        {
            // Arrange
            var appointments = new List<Appointment>
            {
                new Appointment { AppointmentID = 1, TenantID = 1 },
                new Appointment { AppointmentID = 2, TenantID = 1 },
                new Appointment { AppointmentID = 3, TenantID = 2 }
            };

            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                                .Returns((Expression<Func<Appointment, bool>> predicate) => appointments.AsQueryable().Where(predicate).BuildMock());
            
            // Act
            var result = await _manager.GetAllByTenantIdAsync(1);

            // Assert
            result.Count.Should().Be(2);
            result.All(x => x.TenantID == 1).Should().BeTrue();
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_ShouldReturnCorrectCalculatedSlots()
        {
            // Arrange
            int tenantId = 1;
            // Test için yarına bir tarih ayarlıyoruz ve start/end of day için mantıklı bir saat seçiyoruz
            var targetDate = DateTime.Now.AddDays(1).Date; 
            
            var existingAppointments = new List<Appointment>
            {
                // 09:00 - 10:15 (09:00 ile 09:30 dahil dolu)
                new Appointment { TenantID = 1, StartDate = targetDate.AddHours(9).AddMinutes(30), EndDate = targetDate.AddHours(10).AddMinutes(15) }, 
                // 11:00 - 12:00
                new Appointment { TenantID = 1, StartDate = targetDate.AddHours(11), EndDate = targetDate.AddHours(12) }
            };

            await _db.BusinessHours.AddAsync(new BusinessHour
            {
                TenantID = tenantId,
                DayOfWeek = (int)targetDate.DayOfWeek,
                IsClosed = false,
                OpenTime = TimeSpan.FromHours(9),
                CloseTime = TimeSpan.FromHours(18)
            });
            await _db.SaveChangesAsync();

            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                                .Returns((Expression<Func<Appointment, bool>> predicate) => existingAppointments.AsQueryable().Where(predicate).BuildMock());

            // Act
            // 45 dakikalık süre ve 3 boşluk önerisi istiyoruz.
            var result = await _manager.GetAvailableSlotsAsync(tenantId, targetDate, 45, count: 3);

            // Assert
            result.Should().HaveCount(3);
            result[0].Should().Be("10:15"); // 09:00'dan başlar 09:45'te bitmek ister, çakışma var(10:15). Sonra 10:15'ten 11:00'e (uygun).
            result[1].Should().Be("12:00"); // Bir the sonraki uygun boşluk 12:00'dan sonraki.
            result[2].Should().Be("12:15");
        }
    }
}
