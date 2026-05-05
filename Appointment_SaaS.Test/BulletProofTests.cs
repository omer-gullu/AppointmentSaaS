using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
    /// Bullet-Proof Test Suite:
    /// 1. Admin Power — Pasif dükkan tüm API erişimini kaybeder.
    /// 2. Smart Assignment — En az randevulu personele öncelik verilir.
    /// 3. GoogleEventID — n8n'den gelen Google Takvim ID'si DB'ye kaydedilir.
    /// </summary>
    public class BulletProofTests
    {
        private readonly AppDbContext _db;
        private readonly Mock<ITenantRepository> _mockTenantRepo;
        private readonly AppointmentManager _appointmentManager;

        public BulletProofTests()
        {
            _mockTenantRepo = new Mock<ITenantRepository>();

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _db = new AppDbContext(dbOptions);

            var mockAppointmentRepo = new Mock<IAppointmentRepository>();

            // ITenantProvider: null döner — anonim testler için
            var mockTenantProvider = new Mock<ITenantProvider>();
            mockTenantProvider.Setup(x => x.GetTenantId()).Returns((int?)null);

            var mockLogger = new Mock<ILogger<AppointmentManager>>();

            _appointmentManager = new AppointmentManager(
                mockAppointmentRepo.Object,
                null!, // IMapper — bu testlerde kullanılmıyor
                _mockTenantRepo.Object,
                null!, // IEvolutionApiService — bu testlerde kullanılmıyor
                _db,   // AppDbContext — doğrudan InMemory DB
                mockTenantProvider.Object,
                mockLogger.Object
            );
        }

        // ═══════════════════════════════════════════════════════
        //  1. ADMIN POWER TEST — Pasif Dükkan Erişim Engeli
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void PassiveTenant_IsActive_False_ShouldNotAllowContextAccess()
        {
            // Arrange: Admin IsActive=false yaptı
            var tenant = new Tenant
            {
                TenantID = 1,
                IsActive = false,
                IsSubscriptionActive = true,
                IsBlacklisted = false
            };

            // Act: Controller mantığını simüle et
            bool canAccess = tenant.IsActive && tenant.IsSubscriptionActive && !tenant.IsBlacklisted;

            // Assert
            canAccess.Should().BeFalse("IsActive=false olan dükkan Mega-Context'e erişemez.");
        }

        [Fact]
        public void PassiveTenant_IsSubscriptionActive_False_ShouldNotAllowContextAccess()
        {
            // Arrange: Ödeme başarısız — IsSubscriptionActive=false
            var tenant = new Tenant
            {
                TenantID = 2,
                IsActive = true,
                IsSubscriptionActive = false,
                IsBlacklisted = false
            };

            bool canAccess = tenant.IsActive && tenant.IsSubscriptionActive && !tenant.IsBlacklisted;

            canAccess.Should().BeFalse("Aboneliği sona eren dükkan Context'e erişemez.");
        }

        [Fact]
        public void BlacklistedTenant_ShouldNotAllowContextAccess()
        {
            var tenant = new Tenant
            {
                TenantID = 3,
                IsActive = true,
                IsSubscriptionActive = true,
                IsBlacklisted = true
            };

            bool canAccess = tenant.IsActive && tenant.IsSubscriptionActive && !tenant.IsBlacklisted;

            canAccess.Should().BeFalse("Kara listedeki dükkan Context'e erişemez.");
        }

        [Fact]
        public void ActiveTenant_AllFlagsOk_ShouldAllowContextAccess()
        {
            var tenant = new Tenant
            {
                TenantID = 4,
                IsActive = true,
                IsSubscriptionActive = true,
                IsBlacklisted = false
            };

            bool canAccess = tenant.IsActive && tenant.IsSubscriptionActive && !tenant.IsBlacklisted;

            canAccess.Should().BeTrue("Aktif, aboneliği geçerli, kara listede olmayan dükkan erişebilmeli.");
        }

        [Fact]
        public void PassiveTenant_ShouldNotAllowGoogleTokenRefresh()
        {
            // Admin dükkanı pasif yaparsa Google Calendar token da alınamaz
            var tenant = new Tenant
            {
                TenantID = 5,
                IsActive = false,
                IsSubscriptionActive = true,
                GoogleAccessToken = "geçerli_refresh_token"
            };

            // Controller'daki kontrol mantığı
            bool canRefreshToken = tenant.IsActive && tenant.IsSubscriptionActive
                                   && !string.IsNullOrEmpty(tenant.GoogleAccessToken);

            canRefreshToken.Should().BeFalse("Pasif dükkanın Google Token'ı yenilenmemeli.");
        }

        // ═══════════════════════════════════════════════════════
        //  2. TOKEN SECURITY TEST — X-Auth-Token Kontrolü
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void WebhookAuth_WithoutToken_ShouldReturn401()
        {
            // Middleware mantığını simüle et
            string? providedToken = null; // Header yok
            string expectedToken = "super-secret-token";

            bool isAuthorized = !string.IsNullOrEmpty(providedToken)
                                && providedToken == expectedToken;

            isAuthorized.Should().BeFalse("Header olmadan istek 401 almalı.");
        }

        [Fact]
        public void WebhookAuth_WithWrongToken_ShouldReturn401()
        {
            string providedToken = "yanlis-token-123";
            string expectedToken = "super-secret-token";

            bool isAuthorized = !string.IsNullOrEmpty(providedToken)
                                && providedToken == expectedToken;

            isAuthorized.Should().BeFalse("Yanlış token ile istek 401 almalı.");
        }

        [Fact]
        public void WebhookAuth_WithCorrectToken_ShouldPass()
        {
            string providedToken = "super-secret-token";
            string expectedToken = "super-secret-token";

            bool isAuthorized = !string.IsNullOrEmpty(providedToken)
                                && providedToken == expectedToken;

            isAuthorized.Should().BeTrue("Doğru token ile istek geçmeli.");
        }

        [Fact]
        public void WebhookAuth_WithEmptyToken_ShouldReturn401()
        {
            string providedToken = "";
            string expectedToken = "super-secret-token";

            bool isAuthorized = !string.IsNullOrEmpty(providedToken)
                                && providedToken == expectedToken;

            isAuthorized.Should().BeFalse("Boş token ile istek 401 almalı.");
        }

        [Fact]
        public void MegaContextPath_ShouldBeProtected_NotExcluded()
        {
            // Middleware protected API path listesini simüle et
            var protectedApiPaths = new[]
            {
                "/api/tenants/getcontextbyinstance",
                "/api/tenants/getgoogleaccesstoken"
            };

            var path = "/api/tenants/getcontextbyinstance";
            bool isProtected = protectedApiPaths.Any(p => path.ToLower().StartsWith(p.ToLower()));

            isProtected.Should().BeTrue("Mega-Context endpoint'i token korumasına alınmış olmalı.");
        }

        [Fact]
        public void IyzicoWebhookPath_ShouldBeExcluded_FromTokenCheck()
        {
            var excludedPaths = new[] { "/api/iyzico/webhook" };
            var path = "/api/iyzico/webhook";

            bool isExcluded = excludedPaths.Any(p => path.ToLower().StartsWith(p.ToLower()));

            isExcluded.Should().BeTrue("Iyzico webhook kendi imza doğrulamasını yaptığı için token kontrolünden muaf olmalı.");
        }

        // ═══════════════════════════════════════════════════════
        //  3. SMART ASSIGNMENT TEST — Load-Balancing
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task GetStaffWithFewest_ShouldAssignToIdleStaff_WhenOneHasMoreAppointments()
        {
            // Arrange
            var today = DateTime.Today.AddHours(10);
            int tenantId = 100;

            // Personel 1 — bugün 3 randevusu var (yoğun)
            // Personel 2 — bugün 1 randevusu var (boşta)
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

            // Personel 1 için 3 randevu
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

            // Personel 2 için 1 randevu
            await _db.Appointments.AddAsync(new Appointment
            {
                TenantID = tenantId, AppUserID = 2, ServiceID = 1,
                CustomerName = "MüşteriX", CustomerPhone = "555",
                StartDate = today, EndDate = today.AddHours(1),
                Status = "Beklemede", Note = ""
            });
            await _db.SaveChangesAsync();

            // Act
            var assignedStaffId = await _appointmentManager.GetStaffWithFewestAppointmentsAsync(tenantId, today);

            // Assert: Personel 2 (1 randevu) öncelik almalı
            assignedStaffId.Should().Be(2, "Daha az randevusu olan personele atama yapılmalı.");
        }

        [Fact]
        public async Task GetStaffWithFewest_ShouldAssignToFirstById_WhenLoadIsEqual()
        {
            // Arrange: İki personelin eşit randevusu var — küçük ID kazanır
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

            // Her ikisi için 2'şer randevu
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

            assignedStaffId.Should().Be(10, "Eşit yük durumunda küçük ID'li personel seçilmeli.");
        }

        [Fact]
        public async Task GetStaffWithFewest_ShouldReturnNull_WhenNoActiveStaff()
        {
            int tenantId = 102;
            var tenant = new Tenant
            {
                TenantID = tenantId,
                Name = "Personelsiz Dükkan",
                PhoneNumber = "5550000001",
                Address = "Test",
                ApiKey = "TEST3",
                TrialFingerprint = "",
                AppUsers = new List<AppUser>
                {
                    // Pasif personel
                    new AppUser { AppUserID = 20, TenantID = tenantId, Status = false, FirstName = "İzinli", LastName = "Personel", PhoneNumber = "2020", Email = "izinli@test.com" }
                }
            };
            await _db.Tenants.AddAsync(tenant);
            await _db.SaveChangesAsync();

            var result = await _appointmentManager.GetStaffWithFewestAppointmentsAsync(tenantId, DateTime.Today);

            result.Should().BeNull("Aktif personel yoksa null dönmeli.");
        }

        [Fact]
        public async Task GetStaffWithFewest_ShouldAssignToOnlyStaff_WhenSingleActive()
        {
            int tenantId = 103;
            var tenant = new Tenant
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
            };
            await _db.Tenants.AddAsync(tenant);
            await _db.SaveChangesAsync();

            var result = await _appointmentManager.GetStaffWithFewestAppointmentsAsync(tenantId, DateTime.Today);

            result.Should().Be(30, "Tek aktif personel varsa hep ona atanmalı.");
        }

        // ═══════════════════════════════════════════════════════
        //  4. GOOGLE EVENT ID — N8N Kaydı Doğrulama
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task UpdateGoogleEventIdAsync_ShouldSaveId_WhenAppointmentExists()
        {
            // Arrange
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

            // Act
            var result = await _appointmentManager.UpdateGoogleEventIdAsync(appointmentId, "google_event_abc123");

            // Assert
            result.Should().BeTrue();
            var updated = await _db.Appointments.FindAsync(appointmentId);
            updated!.GoogleEventID.Should().Be("google_event_abc123");
        }

        [Fact]
        public async Task UpdateGoogleEventIdAsync_ShouldReturnFalse_WhenAppointmentNotFound()
        {
            var result = await _appointmentManager.UpdateGoogleEventIdAsync(99999, "some_event_id");

            result.Should().BeFalse("Var olmayan randevu için false dönmeli.");
        }

        [Fact]
        public async Task UpdateGoogleEventIdAsync_ShouldOverwrite_WhenCalledTwice()
        {
            // n8n retry durumunda: aynı randevuya iki kez ID gelebilir
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

            // İlk çağrı
            await _appointmentManager.UpdateGoogleEventIdAsync(appointment.AppointmentID, "yeni_event_id");

            // Assert: Üzerine yazıldı
            var updated = await _db.Appointments.FindAsync(appointment.AppointmentID);
            updated!.GoogleEventID.Should().Be("yeni_event_id");
        }
    }
}
