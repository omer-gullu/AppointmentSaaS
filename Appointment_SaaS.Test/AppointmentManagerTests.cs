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
using Appointment_SaaS.Test.TestHelpers;
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
        private readonly int _staffId;
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

            var mockCache = new Mock<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            var mockGoogle = new Mock<IGoogleCalendarService>();

            _manager = new AppointmentManager(
                _mockAppointmentRepo.Object,
                _mockMapper.Object,
                _mockTenantRepo.Object,
                _mockEvolutionService.Object,
                _db,
                mockTenantProvider.Object,
                mockLogger.Object,
                mockCache.Object,
                mockGoogle.Object
            );

            _staffId = AppointmentTestSeeds.EnsureStaffWithGoogleAsync(_db).GetAwaiter().GetResult();
            mockGoogle
                .Setup(x => x.AddEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync("google_event_test");
        }

        [Fact]
        public async Task AddAppointmentAsync_ShouldThrowException_WhenSlotIsOccupied()
        {
            // Arrange
            var dto = new AppointmentCreateDto
            {
                TenantID = 1,
                AppUserID = _staffId,
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
                    AppUserID = _staffId,
                    StartDate = dto.StartDate, 
                    EndDate = dto.EndDate 
                }
            };
            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                                .Returns((Expression<Func<Appointment, bool>> predicate) => appointments.AsQueryable().Where(predicate).BuildMock());

            _mockMapper.Setup(m => m.Map<Appointment>(dto)).Returns(new Appointment { StartDate = dto.StartDate, EndDate = dto.EndDate });

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
        public async Task AddAppointmentAsync_ShouldThrowException_WhenDateIsHoliday()
        {
            var start = DateTime.Now.AddDays(30).Date;
            while (start.DayOfWeek == DayOfWeek.Sunday)
                start = start.AddDays(1);
            start = start.AddHours(10);

            var dto = new AppointmentCreateDto
            {
                TenantID = 1,
                AppUserID = _staffId,
                StartDate = start,
                EndDate = start.AddHours(1),
            };

            var tenant = new Tenant
            {
                TenantID = 1,
                BusinessHours = new List<BusinessHour>
                {
                    new BusinessHour
                    {
                        DayOfWeek = (int)start.DayOfWeek,
                        IsClosed = false,
                        OpenTime = TimeSpan.FromHours(9),
                        CloseTime = TimeSpan.FromHours(18),
                    },
                },
                Holidays = new List<Holiday>
                {
                    new Holiday { TenantId = 1, Date = DateOnly.FromDateTime(start.Date), Name = "23 Nisan Ulusal Egemenlik" },
                },
            };

            _mockTenantRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .Returns((Expression<Func<Tenant, bool>> predicate) => new List<Tenant> { tenant }.AsQueryable().Where(predicate).BuildMock());

            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                .Returns((Expression<Func<Appointment, bool>> predicate) => new List<Appointment>().AsQueryable().Where(predicate).BuildMock());

            Func<Task> act = async () => await _manager.AddAppointmentAsync(dto);

            await act.Should().ThrowAsync<BadHttpRequestException>()
                .WithMessage("*23 Nisan Ulusal Egemenlik*nedeniyle randevu verilemez*");

            _mockAppointmentRepo.Verify(x => x.AddAsync(It.IsAny<Appointment>()), Times.Never);
        }

        [Fact]
        public async Task AddAppointmentAsync_ShouldCallNotificationService_WhenSuccess()
        {
            // Arrange
            var dto = new AppointmentCreateDto
            {
                TenantID = 1,
                AppUserID = _staffId,
                StartDate = DateTime.Now.AddDays(1).Date.AddHours(10),
                EndDate = DateTime.Now.AddDays(1).Date.AddHours(11),
                Note = "Test Randevu",
                CustomerPhone = "05551234567",
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
            _mockEvolutionService.Verify(x => x.SendWhatsAppMessageAsync($"tenant_{dto.TenantID}", "05551234567", It.Is<string>(s => s.Contains("başarıyla oluşturulmuştur"))), Times.Once);
        }

        [Fact]
        public async Task AddAppointmentAsync_ShouldPersistTwoAppointmentServiceLinks_WhenServiceIdsHasTwoEntries()
        {
            _db.Services.Add(new Service { ServiceID = 1, TenantID = 1, Name = "Saç", DurationInMinutes = 30, Price = 100 });
            _db.Services.Add(new Service { ServiceID = 2, TenantID = 1, Name = "Sakal", DurationInMinutes = 15, Price = 50 });
            await _db.SaveChangesAsync();

            var start = DateTime.Now.AddDays(1).Date.AddHours(10);
            var dto = new AppointmentCreateDto
            {
                TenantID = 1,
                AppUserID = _staffId,
                ServiceID = 1,
                ServiceIds = new List<int> { 1, 2 },
                StartDate = start,
                EndDate = start.AddMinutes(45),
                Note = "Multi",
                CustomerPhone = "5550000000",
                CustomerName = "Veli"
            };

            var tenant = new Tenant
            {
                TenantID = 1,
                BusinessHours = new List<BusinessHour>
                {
                    new BusinessHour
                    {
                        DayOfWeek = (int)start.DayOfWeek,
                        IsClosed = false,
                        OpenTime = TimeSpan.FromHours(8),
                        CloseTime = TimeSpan.FromHours(20)
                    }
                }
            };

            _mockTenantRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .Returns((Expression<Func<Tenant, bool>> predicate) => new List<Tenant> { tenant }.AsQueryable().Where(predicate).BuildMock());

            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                .Returns((Expression<Func<Appointment, bool>> predicate) => new List<Appointment>().AsQueryable().Where(predicate).BuildMock());

            var fakeAppointment = new Appointment { AppointmentID = 501, StartDate = dto.StartDate };
            _mockMapper.Setup(m => m.Map<Appointment>(dto)).Returns(fakeAppointment);

            _mockEvolutionService.Setup(x => x.SendWhatsAppMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var id = await _manager.AddAppointmentAsync(dto);

            id.Should().Be(501);
            var links = await _db.AppointmentServiceLinks.Where(l => l.AppointmentID == 501).OrderBy(l => l.SortOrder).ToListAsync();
            links.Should().HaveCount(2);
            links[0].ServiceID.Should().Be(1);
            links[1].ServiceID.Should().Be(2);
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
                    AppUserID = _staffId,
                    StartDate = new DateTime(2030, 1, 1, 10, 30, 0), // 10:30-11:30 (Overlap with 10:00-11:00)
                    EndDate = new DateTime(2030, 1, 1, 11, 30, 0)
                }
            };

            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                                .Returns((Expression<Func<Appointment, bool>> predicate) => existingAppointments.AsQueryable().Where(predicate).BuildMock());

            // Act
            var result = await _manager.IsSlotAvailableAsync(tenantId, 1, targetStart, targetEnd);

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
                    AppUserID = _staffId,
                    StartDate = new DateTime(2030, 1, 1, 11, 0, 0), // 11:00-12:00 (No Overlap with 10:00-11:00)
                    EndDate = new DateTime(2030, 1, 1, 12, 0, 0)
                }
            };

            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                                .Returns((Expression<Func<Appointment, bool>> predicate) => existingAppointments.AsQueryable().Where(predicate).BuildMock());

            // Act
            var result = await _manager.IsSlotAvailableAsync(tenantId, 1, targetStart, targetEnd);

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
            result[2].Should().Be("12:45");
        }

        [Fact]
        public async Task GetActiveAppointmentsByPhoneAsync_ShouldReturnBothRows_WhenOnePhoneIsWhatsAppJid()
        {
            var sector = new Sector { Name = "Sec", CreatedAt = DateTime.UtcNow };
            _db.Sectors.Add(sector);
            await _db.SaveChangesAsync();
            var sectorId = sector.SectorID;

            var tenant = new Tenant
            {
                Name = "Shop",
                PhoneNumber = "05000000000",
                Address = "A",
                ApiKey = "k",
                CreatedAt = DateTime.UtcNow,
                SectorID = sectorId,
                IsActive = true,
                IsTrial = false,
                SubscriptionEndDate = DateTime.UtcNow.AddDays(30),
                IsSubscriptionActive = true,
                TrialFingerprint = "fp"
            };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync();
            var tenantId = tenant.TenantID;

            var user = new AppUser
            {
                FirstName = "S",
                LastName = "U",
                Email = "s@u.com",
                PhoneNumber = "05000000001",
                TenantID = tenantId,
                Status = true,
                Appointments = new List<Appointment>()
            };
            _db.AppUsers.Add(user);
            await _db.SaveChangesAsync();
            var uid = user.AppUserID;

            var service = new Service { Name = "Lazer", TenantID = tenantId, DurationInMinutes = 15, Price = 100 };
            _db.Services.Add(service);
            await _db.SaveChangesAsync();
            var serviceId = service.ServiceID;

            var t1 = DateTime.Now.AddHours(2);
            var t2 = DateTime.Now.AddHours(3);
            _db.Appointments.Add(new Appointment
            {
                TenantID = tenantId,
                AppUserID = uid,
                ServiceID = serviceId,
                CustomerName = "Müşteri",
                CustomerPhone = "905317239931@s.whatsapp.net",
                StartDate = t1,
                EndDate = t1.AddMinutes(15),
                Status = "Beklemede",
                Note = "n",
            });
            _db.Appointments.Add(new Appointment
            {
                TenantID = tenantId,
                AppUserID = uid,
                ServiceID = serviceId,
                CustomerName = "Müşteri",
                CustomerPhone = "05317239931",
                StartDate = t2,
                EndDate = t2.AddMinutes(15),
                Status = "Beklemede",
                Note = "n",
            });
            await _db.SaveChangesAsync();

            var result = await _manager.GetActiveAppointmentsByPhoneAsync("05317239931", tenantId);

            result.Should().HaveCount(2);

            var history = await _manager.GetCustomerHistoryAsync("05317239931", tenantId);
            history.IsReturningCustomer.Should().BeTrue();
            history.TotalVisits.Should().Be(2);
        }

        [Fact]
        public async Task GetActiveAppointmentsByPhoneAsync_ShouldExcludeCancelled()
        {
            var sector = new Sector { Name = "Sec", CreatedAt = DateTime.UtcNow };
            _db.Sectors.Add(sector);
            await _db.SaveChangesAsync();

            var tenant = new Tenant
            {
                Name = "T",
                PhoneNumber = "05000000000",
                Address = "A",
                ApiKey = "k",
                CreatedAt = DateTime.UtcNow,
                SectorID = sector.SectorID,
                IsActive = true,
                IsSubscriptionActive = true,
                TrialFingerprint = "fp"
            };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync();

            var user = new AppUser
            {
                FirstName = "A",
                LastName = "B",
                Email = "a@b.com",
                PhoneNumber = "05000000001",
                TenantID = tenant.TenantID,
                Status = true
            };
            _db.AppUsers.Add(user);
            await _db.SaveChangesAsync();

            var service = new Service { Name = "X", TenantID = tenant.TenantID, DurationInMinutes = 30, Price = 100 };
            _db.Services.Add(service);
            await _db.SaveChangesAsync();

            var start = DateTime.Now.AddHours(1);
            _db.Appointments.Add(new Appointment
            {
                TenantID = tenant.TenantID,
                AppUserID = user.AppUserID,
                ServiceID = service.ServiceID,
                CustomerPhone = "05551112233",
                CustomerName = "Ali",
                StartDate = start,
                EndDate = start.AddMinutes(30),
                Status = "İptal",
                Note = ""
            });
            await _db.SaveChangesAsync();

            var result = await _manager.GetActiveAppointmentsByPhoneAsync("05551112233", tenant.TenantID);
            result.Should().BeEmpty();
        }

        // ─── UpdateAsync slot conflict (n8n PUT akışı için kritik) ─────────────
        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenNewSlotConflictsWithAnotherAppointment()
        {
            var future = DateTime.Now.AddDays(1).Date.AddHours(10);
            var appointment = new Appointment
            {
                AppointmentID = 100,
                TenantID = 1,
                AppUserID = _staffId,
                ServiceID = 1,
                CustomerName = "Ali",
                CustomerPhone = "05551234567",
                StartDate = future,
                EndDate = future.AddMinutes(30),
                Status = "Beklemede",
                Note = ""
            };

            var tenant = new Tenant
            {
                TenantID = 1,
                BusinessHours = new List<BusinessHour>
                {
                    new BusinessHour
                    {
                        DayOfWeek = (int)future.DayOfWeek,
                        IsClosed = false,
                        OpenTime = TimeSpan.FromHours(9),
                        CloseTime = TimeSpan.FromHours(18)
                    }
                }
            };
            _mockTenantRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .Returns((Expression<Func<Tenant, bool>> predicate) => new List<Tenant> { tenant }.AsQueryable().Where(predicate).BuildMock());

            var conflicting = new List<Appointment>
            {
                new Appointment
                {
                    AppointmentID = 200,
                    TenantID = 1,
                    AppUserID = _staffId,
                    StartDate = future,
                    EndDate = future.AddMinutes(30)
                }
            };
            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                .Returns((Expression<Func<Appointment, bool>> predicate) => conflicting.AsQueryable().Where(predicate).BuildMock());

            Func<Task> act = async () => await _manager.UpdateAsync(appointment);

            await act.Should().ThrowAsync<BadHttpRequestException>()
                .WithMessage("Seçilen saat dilimi başka bir randevu ile çakışıyor.");
            _mockAppointmentRepo.Verify(x => x.Update(It.IsAny<Appointment>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_ShouldNotThrow_WhenOnlyConflictIsItself()
        {
            var future = DateTime.Now.AddDays(1).Date.AddHours(10);
            var appointment = new Appointment
            {
                AppointmentID = 100,
                TenantID = 1,
                AppUserID = _staffId,
                ServiceID = 1,
                CustomerName = "Ali",
                CustomerPhone = "05551234567",
                StartDate = future,
                EndDate = future.AddMinutes(30),
                Status = "Beklemede",
                Note = ""
            };

            var tenant = new Tenant
            {
                TenantID = 1,
                BusinessHours = new List<BusinessHour>
                {
                    new BusinessHour
                    {
                        DayOfWeek = (int)future.DayOfWeek,
                        IsClosed = false,
                        OpenTime = TimeSpan.FromHours(9),
                        CloseTime = TimeSpan.FromHours(18)
                    }
                }
            };
            _mockTenantRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .Returns((Expression<Func<Tenant, bool>> predicate) => new List<Tenant> { tenant }.AsQueryable().Where(predicate).BuildMock());

            var onlySelf = new List<Appointment>
            {
                new Appointment
                {
                    AppointmentID = 100,
                    TenantID = 1,
                    AppUserID = _staffId,
                    StartDate = future,
                    EndDate = future.AddMinutes(30)
                }
            };
            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                .Returns((Expression<Func<Appointment, bool>> predicate) => onlySelf.AsQueryable().Where(predicate).BuildMock());

            await _manager.UpdateAsync(appointment);

            _mockAppointmentRepo.Verify(x => x.Update(appointment), Times.Once);
            _mockAppointmentRepo.Verify(x => x.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenStaffChangedAndAddSucceeds_ShouldDeleteOldCalendarEvent()
        {
            var future = DateTime.Now.AddDays(1).Date.AddHours(10);
            var appointment = new Appointment
            {
                AppointmentID = 50,
                TenantID = 1,
                AppUserID = 2,
                ServiceID = 1,
                CustomerName = "Ali",
                CustomerPhone = "05551234567",
                StartDate = future,
                EndDate = future.AddMinutes(30),
                Status = "Beklemede",
                Note = "",
                GoogleEventID = "old-event-id"
            };

            var tenant = new Tenant
            {
                TenantID = 1,
                BusinessHours = new List<BusinessHour>
                {
                    new BusinessHour
                    {
                        DayOfWeek = (int)future.DayOfWeek,
                        IsClosed = false,
                        OpenTime = TimeSpan.FromHours(9),
                        CloseTime = TimeSpan.FromHours(18)
                    }
                }
            };
            _mockTenantRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .Returns((Expression<Func<Tenant, bool>> predicate) => new List<Tenant> { tenant }.AsQueryable().Where(predicate).BuildMock());
            _mockAppointmentRepo.Setup(x => x.Where(It.IsAny<Expression<Func<Appointment, bool>>>()))
                .Returns((Expression<Func<Appointment, bool>> predicate) => new List<Appointment>().AsQueryable().Where(predicate).BuildMock());

            var mockGoogle = new Mock<IGoogleCalendarService>();
            mockGoogle.Setup(x => x.AddEventAsync(2, It.IsAny<string>(), It.IsAny<string>(), future, future.AddMinutes(30)))
                .ReturnsAsync("new-event-id");
            mockGoogle.Setup(x => x.DeleteEventAsync(1, "old-event-id"))
                .Returns(Task.CompletedTask);

            var manager = new AppointmentManager(
                _mockAppointmentRepo.Object,
                _mockMapper.Object,
                _mockTenantRepo.Object,
                _mockEvolutionService.Object,
                _db,
                new Mock<ITenantProvider>().Object,
                new Mock<ILogger<AppointmentManager>>().Object,
                new Mock<Microsoft.Extensions.Caching.Memory.IMemoryCache>().Object,
                mockGoogle.Object);

            await manager.UpdateAsync(appointment, previousAppUserID: 1);

            appointment.GoogleEventID.Should().Be("new-event-id");
            mockGoogle.Verify(x => x.AddEventAsync(2, It.IsAny<string>(), It.IsAny<string>(), future, future.AddMinutes(30)), Times.Once);
            mockGoogle.Verify(x => x.DeleteEventAsync(1, "old-event-id"), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrow_WhenStartDateIsInPast()
        {
            var past = DateTime.Now.AddHours(-1);
            var appointment = new Appointment
            {
                AppointmentID = 100,
                TenantID = 1,
                AppUserID = _staffId,
                ServiceID = 1,
                CustomerName = "Ali",
                CustomerPhone = "05551234567",
                StartDate = past,
                EndDate = past.AddMinutes(30),
                Status = "Beklemede",
                Note = ""
            };

            Func<Task> act = async () => await _manager.UpdateAsync(appointment);

            await act.Should().ThrowAsync<BadHttpRequestException>()
                .WithMessage("Geçmiş bir tarihe randevu güncellenemez.");
        }
    }
}
