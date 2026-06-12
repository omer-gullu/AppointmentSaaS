using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Appointment_SaaS.API.Authorization;
using Appointment_SaaS.API.Controllers;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Appointment_SaaS.Test
{
    /// <summary>
    /// API Katmanı (Controller) IDOR ve Yetkilendirme Testleri:
    /// Bir tenant kullanıcısının, başka bir tenant'a ait verilere
    /// (randevu, işletme bilgisi vb.) erişiminin Controller seviyesinde
    /// 403 Forbidden veya 401 Unauthorized ile engellendiğini doğrular.
    /// </summary>
    public class ControllerSecurityTests
    {
        private ClaimsPrincipal CreateMockUser(string role, int tenantId, string authType = "Bearer")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, role),
                new Claim("TenantId", tenantId.ToString())
            };
            var identity = new ClaimsIdentity(claims, authType);
            return new ClaimsPrincipal(identity);
        }

        private ControllerContext CreateMockControllerContext(ClaimsPrincipal user)
        {
            var httpContext = new DefaultHttpContext { User = user };
            return new ControllerContext { HttpContext = httpContext };
        }

        private ControllerContext CreateWebhookScopedContext(int scopedTenantId)
        {
            var user = CreateMockUser("Webhook", scopedTenantId, "WebhookScheme");
            var httpContext = new DefaultHttpContext { User = user };
            httpContext.Items[WebhookContextKeys.TenantId] = scopedTenantId;
            return new ControllerContext { HttpContext = httpContext };
        }

        // ═══════════════════════════════════════════════════════
        //  1. APPOINTMENTS CONTROLLER IDOR TESTLERİ
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task AppointmentsController_GetByTenant_ShouldReturn403_WhenTenantIdMismatches()
        {
            // Arrange
            var mockApptService = new Mock<IAppointmentService>();
            var mockTenantService = new Mock<ITenantService>();
            var mockServiceService = new Mock<IServiceService>();
            var mockAppUserService = new Mock<IAppUserService>();
            var mockLogger = new Mock<ILogger<AppointmentsController>>();

            var controller = new AppointmentsController(
                mockApptService.Object,
                mockTenantService.Object,
                mockServiceService.Object,
                mockAppUserService.Object,
                mockLogger.Object
            );

            // Giriş yapan kullanıcı TenantID = 5
            controller.ControllerContext = CreateMockControllerContext(CreateMockUser("Manager", 5));

            // Act: Kullanıcı TenantID = 10 olan randevuları istiyor
            var result = await controller.GetByTenant(10);

            // Assert: IDOR koruması devreye girmeli ve 403 Forbidden dönmeli
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(403);

            // Servis hiç çağrılmamalı (veritabanına gidilmeden API katmanında reddedilmeli)
            mockApptService.Verify(x => x.GetListItemsByTenantIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task AppointmentsController_GetByTenant_ShouldReturnOk_WhenTenantIdMatches()
        {
            // Arrange
            var mockApptService = new Mock<IAppointmentService>();
            mockApptService.Setup(x => x.GetListItemsByTenantIdAsync(5))
                           .ReturnsAsync(new List<AppointmentListItemDto> { new() { AppointmentID = 1 } });

            var mockTenantService = new Mock<ITenantService>();
            var mockServiceService = new Mock<IServiceService>();
            var mockAppUserService = new Mock<IAppUserService>();
            var mockLogger = new Mock<ILogger<AppointmentsController>>();

            var controller = new AppointmentsController(
                mockApptService.Object,
                mockTenantService.Object,
                mockServiceService.Object,
                mockAppUserService.Object,
                mockLogger.Object
            );

            // Giriş yapan kullanıcı TenantID = 5
            controller.ControllerContext = CreateMockControllerContext(CreateMockUser("Manager", 5));

            // Act: Kullanıcı kendi (TenantID = 5) randevularını istiyor
            var result = await controller.GetByTenant(5);

            // Assert: Başarılı olmalı
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(200);
        }

        [Fact]
        public async Task AppointmentsController_GetByTenant_ShouldAllowAccess_WhenAuthIsWebhook()
        {
            // Arrange
            var mockApptService = new Mock<IAppointmentService>();
            mockApptService.Setup(x => x.GetListItemsByTenantIdAsync(99))
                           .ReturnsAsync(new List<AppointmentListItemDto> { new() { AppointmentID = 2 } });

            var mockTenantService = new Mock<ITenantService>();
            var mockServiceService = new Mock<IServiceService>();
            var mockAppUserService = new Mock<IAppUserService>();
            var mockLogger = new Mock<ILogger<AppointmentsController>>();

            var controller = new AppointmentsController(
                mockApptService.Object,
                mockTenantService.Object,
                mockServiceService.Object,
                mockAppUserService.Object,
                mockLogger.Object
            );

            controller.ControllerContext = CreateWebhookScopedContext(99);

            // Act
            var result = await controller.GetByTenant(99);

            // Assert: Webhook kapsamı eşleşince 200
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(200);
        }

        // ═══════════════════════════════════════════════════════
        //  2. TENANTS CONTROLLER IDOR TESTLERİ
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task TenantsController_Update_ShouldReturn403_WhenTenantUpdatesAnotherTenant()
        {
            // Arrange
            var mockTenantService = new Mock<ITenantService>();
            mockTenantService.Setup(x => x.GetByIdAsync(20)).ReturnsAsync(new Tenant { TenantID = 20 });

            var mockConfig = new Mock<IConfiguration>();
            var mockHttpClientFactory = new Mock<System.Net.Http.IHttpClientFactory>();
            var mockIyzicoService = new Mock<IIyzicoPaymentService>();
            var mockAppUserService = new Mock<IAppUserService>();

            var controller = new TenantsController(
                mockTenantService.Object,
                mockConfig.Object,
                mockHttpClientFactory.Object,
                mockIyzicoService.Object,
                mockAppUserService.Object,
                new Mock<ITenantPlanService>().Object
            );

            // Giriş yapan kullanıcı TenantID = 10, Rolü = Manager (Admin DEĞİL)
            controller.ControllerContext = CreateMockControllerContext(CreateMockUser("Manager", 10));

            // Act: Kullanıcı TenantID = 20 olan işletmeyi güncellemeyi deniyor
            var dto = new TenantUpdateDto { Name = "Hacked Name" };
            var result = await controller.Update(20, dto);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(403);
            
            // Servis update metodu asla çağrılmamalı
            mockTenantService.Verify(x => x.UpdateAsync(It.IsAny<Tenant>()), Times.Never);
        }

        [Fact]
        public async Task TenantsController_Update_ShouldAllowAccess_WhenUserIsAdmin()
        {
            // Arrange
            var mockTenantService = new Mock<ITenantService>();
            var targetTenant = new Tenant { TenantID = 20, Name = "Old Name" };
            mockTenantService.Setup(x => x.GetByIdAsync(20)).ReturnsAsync(targetTenant);

            var mockConfig = new Mock<IConfiguration>();
            var mockHttpClientFactory = new Mock<System.Net.Http.IHttpClientFactory>();
            var mockIyzicoService = new Mock<IIyzicoPaymentService>();
            var mockAppUserService = new Mock<IAppUserService>();

            var controller = new TenantsController(
                mockTenantService.Object,
                mockConfig.Object,
                mockHttpClientFactory.Object,
                mockIyzicoService.Object,
                mockAppUserService.Object,
                new Mock<ITenantPlanService>().Object
            );

            // Giriş yapan kullanıcı Admin
            controller.ControllerContext = CreateMockControllerContext(CreateMockUser("Admin", 1));

            // Act: Admin, Tenant 20'yi güncelliyor
            var dto = new TenantUpdateDto { Name = "New Name" };
            var result = await controller.Update(20, dto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.StatusCode.Should().Be(200);

            targetTenant.Name.Should().Be("New Name");
            mockTenantService.Verify(x => x.UpdateAsync(targetTenant), Times.Once);
        }

        [Fact]
        public async Task TenantsController_Update_ManagerCannotToggleIsActive()
        {
            var mockTenantService = new Mock<ITenantService>();
            var targetTenant = new Tenant { TenantID = 5, Name = "Shop", IsActive = false };
            mockTenantService.Setup(x => x.GetByIdAsync(5)).ReturnsAsync(targetTenant);

            var controller = new TenantsController(
                mockTenantService.Object,
                new Mock<IConfiguration>().Object,
                new Mock<System.Net.Http.IHttpClientFactory>().Object,
                new Mock<IIyzicoPaymentService>().Object,
                new Mock<IAppUserService>().Object,
                new Mock<ITenantPlanService>().Object);

            controller.ControllerContext = CreateMockControllerContext(CreateMockUser("Manager", 5));

            var dto = new TenantUpdateDto { IsActive = true, Name = "Shop Updated" };
            var result = await controller.Update(5, dto);

            result.Should().BeOfType<OkObjectResult>();
            targetTenant.IsActive.Should().BeFalse();
            targetTenant.Name.Should().Be("Shop Updated");
        }

        private static AppointmentsController CreateAppointmentsController(
            Mock<IAppointmentService> mockAppt,
            Mock<ITenantService>? mockTenant = null)
        {
            return new AppointmentsController(
                mockAppt.Object,
                (mockTenant ?? new Mock<ITenantService>()).Object,
                new Mock<IServiceService>().Object,
                new Mock<IAppUserService>().Object,
                new Mock<ILogger<AppointmentsController>>().Object);
        }

        [Fact]
        public async Task AppointmentsController_Update_ShouldReturn403_WhenAppointmentBelongsToAnotherTenant()
        {
            var mockAppt = new Mock<IAppointmentService>();
            mockAppt.Setup(x => x.GetByIdAsync(100))
                .ReturnsAsync(new Appointment { AppointmentID = 100, TenantID = 20, AppUserID = 1, ServiceID = 1 });

            var controller = CreateAppointmentsController(mockAppt);
            controller.ControllerContext = CreateMockControllerContext(CreateMockUser("Manager", 5));

            var result = await controller.Update(100, new AppointmentCreateDto
            {
                ServiceID = 1,
                StartDate = DateTime.Now.AddDays(1),
                CustomerName = "X",
                CustomerPhone = "05000000000"
            });

            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(403);
            mockAppt.Verify(x => x.UpdateAsync(It.IsAny<Appointment>(), It.IsAny<int>(), It.IsAny<List<int>>()), Times.Never);
        }

        [Fact]
        public async Task AppointmentsController_Delete_ShouldReturn403_WhenAppointmentBelongsToAnotherTenant()
        {
            var mockAppt = new Mock<IAppointmentService>();
            mockAppt.Setup(x => x.GetByIdAsync(101))
                .ReturnsAsync(new Appointment { AppointmentID = 101, TenantID = 20 });

            var controller = CreateAppointmentsController(mockAppt);
            controller.ControllerContext = CreateMockControllerContext(CreateMockUser("Manager", 5));

            var result = await controller.Delete(101);

            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(403);
            mockAppt.Verify(x => x.DeleteAsync(It.IsAny<Appointment>()), Times.Never);
        }

        [Fact]
        public async Task AppointmentsController_Delete_ShouldAllowWebhook_WhenScopedTenantMatches()
        {
            var mockAppt = new Mock<IAppointmentService>();
            var appt = new Appointment { AppointmentID = 102, TenantID = 99 };
            mockAppt.Setup(x => x.GetByIdAsync(102)).ReturnsAsync(appt);

            var controller = CreateAppointmentsController(mockAppt);
            controller.ControllerContext = CreateWebhookScopedContext(99);

            var result = await controller.Delete(102);

            result.Should().BeOfType<OkObjectResult>();
            mockAppt.Verify(x => x.DeleteAsync(appt), Times.Once);
        }

        [Fact]
        public async Task AppointmentsController_Delete_ShouldReturn403_WhenWebhookScopeMismatches()
        {
            var mockAppt = new Mock<IAppointmentService>();
            var appt = new Appointment { AppointmentID = 102, TenantID = 99 };
            mockAppt.Setup(x => x.GetByIdAsync(102)).ReturnsAsync(appt);

            var controller = CreateAppointmentsController(mockAppt);
            controller.ControllerContext = CreateWebhookScopedContext(10);

            var result = await controller.Delete(102);

            var forbidden = result.Should().BeOfType<ObjectResult>().Subject;
            forbidden.StatusCode.Should().Be(403);
            mockAppt.Verify(x => x.DeleteAsync(It.IsAny<Appointment>()), Times.Never);
        }

        // ═══════════════════════════════════════════════════════
        //  3. SERVICES CONTROLLER IDOR TESTLERİ
        // ═══════════════════════════════════════════════════════

        private static ServicesController CreateServicesController(Mock<IServiceService> mockService)
        {
            return new ServicesController(mockService.Object, new Mock<ITenantService>().Object);
        }

        [Fact]
        public async Task ServicesController_Create_ShouldForceTenantId_FromJwtClaim()
        {
            var mockService = new Mock<IServiceService>();
            ServiceCreateDto? captured = null;
            mockService.Setup(x => x.AddServiceAsync(It.IsAny<ServiceCreateDto>()))
                .Callback<ServiceCreateDto>(d => captured = d)
                .ReturnsAsync(1);

            var controller = CreateServicesController(mockService);
            controller.ControllerContext = CreateMockControllerContext(CreateMockUser("Manager", 7));

            await controller.Create(new ServiceCreateDto
            {
                Name = "Lazer",
                TenantID = 999,
                DurationMinutes = 30,
                Price = 100
            });

            captured.Should().NotBeNull();
            captured!.TenantID.Should().Be(7);
        }

        [Fact]
        public async Task ServicesController_Update_ShouldReturn403_WhenServiceBelongsToAnotherTenant()
        {
            var mockService = new Mock<IServiceService>();
            mockService.Setup(x => x.GetByIdAsync(5))
                .ReturnsAsync(new Service { ServiceID = 5, TenantID = 30, Name = "A", DurationInMinutes = 30, Price = 1 });

            var controller = CreateServicesController(mockService);
            controller.ControllerContext = CreateMockControllerContext(CreateMockUser("Manager", 7));

            var result = await controller.Update(5, new ServiceCreateDto { Name = "Hack", DurationMinutes = 30, Price = 1 });

            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(403);
            mockService.Verify(x => x.UpdateAsync(It.IsAny<Service>()), Times.Never);
        }

        [Fact]
        public async Task ServicesController_Delete_ShouldReturn403_WhenServiceBelongsToAnotherTenant()
        {
            var mockService = new Mock<IServiceService>();
            mockService.Setup(x => x.GetByIdAsync(6))
                .ReturnsAsync(new Service { ServiceID = 6, TenantID = 30 });

            var controller = CreateServicesController(mockService);
            controller.ControllerContext = CreateMockControllerContext(CreateMockUser("Manager", 7));

            var result = await controller.Delete(6);

            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(403);
            mockService.Verify(x => x.DeleteAsync(It.IsAny<Service>()), Times.Never);
        }

        // ═══════════════════════════════════════════════════════
        //  4. APP USERS — GOOGLE TOKEN
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task AppUsersController_GetGoogleToken_ShouldNotReturnStaff_FromAnotherTenant()
        {
            var mockAppUser = new Mock<IAppUserService>();
            mockAppUser.Setup(x => x.GetStaffByTenantAsync(5))
                .ReturnsAsync(new List<AppUser>
                {
                    new AppUser { AppUserID = 1, TenantID = 5, GoogleRefreshToken = "r", GoogleCalendarId = "c@x.com" }
                });

            var mockTenantProvider = new Mock<Appointment_SaaS.Core.Services.ITenantProvider>();
            mockTenantProvider.Setup(x => x.GetTenantId()).Returns(5);

            var controller = new AppUsersController(
                mockAppUser.Object,
                new Mock<IAuthService>().Object,
                new Mock<ITenantService>().Object,
                mockTenantProvider.Object,
                new Mock<IUserOperationClaimService>().Object,
                new Mock<IConfiguration>().Object,
                new Mock<System.Net.Http.IHttpClientFactory>().Object);

            controller.ControllerContext = CreateMockControllerContext(CreateMockUser("Manager", 5));

            // Staff 99 başka tenant'ta; tenant 5 listesinde yok
            var result = await controller.GetGoogleToken(99);

            result.Should().BeOfType<BadRequestObjectResult>();
            mockAppUser.Verify(x => x.GetAllUsersAsync(), Times.Never);
        }
    }
}
