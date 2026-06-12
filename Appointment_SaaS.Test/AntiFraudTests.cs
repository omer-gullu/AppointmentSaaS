using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using Appointment_SaaS.Core.Utilities.Security.JWT;
using Appointment_SaaS.Core.Utilities.Security.Jwt;
using Appointment_SaaS.Core.Utilities.Security;
using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Appointment_SaaS.Test.TestHelpers;

namespace Appointment_SaaS.Test
{
    /// <summary>
    /// Anti-Fraud, Blacklist ve Fingerprint senaryolarını test eder.
    /// </summary>
    public class AntiFraudTests
    {
        private readonly Mock<IAppUserService> _mockUserService;
        private readonly Mock<ITenantService> _mockTenantService;
        private readonly Mock<ITokenHelper> _mockTokenHelper;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IEvolutionApiService> _mockEvolutionApiService;
        private readonly Mock<IUserOperationClaimService> _mockUserOpClaimService;
        private readonly Mock<IIyzicoPaymentService> _mockIyzicoPaymentService;
        private readonly Mock<ITenantPlanService> _mockTenantPlanService;
        private readonly AuthManager _authManager;

        public AntiFraudTests()
        {
            _mockUserService = new Mock<IAppUserService>();
            _mockTenantService = new Mock<ITenantService>();
            _mockTokenHelper = new Mock<ITokenHelper>();
            _mockMapper = new Mock<IMapper>();
            _mockEvolutionApiService = new Mock<IEvolutionApiService>();
            _mockUserOpClaimService = new Mock<IUserOperationClaimService>();
            _mockIyzicoPaymentService = new Mock<IIyzicoPaymentService>();
            _mockTenantPlanService = new Mock<ITenantPlanService>();
            _mockTenantPlanService
                .Setup(x => x.TryReconcileFromIyzicoAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var evoOptions = new Mock<IOptions<EvolutionApiSettings>>();
            evoOptions.Setup(x => x.Value).Returns(new EvolutionApiSettings { DefaultInstance = "default" });

            var iyzicoOptions = new Mock<IOptions<IyzicoSettings>>();
            iyzicoOptions.Setup(x => x.Value).Returns(new IyzicoSettings { Enabled = false });

            var lockoutOptions = new Mock<IOptions<LockoutSettings>>();
            lockoutOptions.Setup(x => x.Value).Returns(new LockoutSettings
            {
                MaxFailedAccessAttempts = 5,
                DefaultLockoutTimeSpanInMinutes = 10
            });

            var logger = new Mock<ILogger<AuthManager>>();
            var hostEnv = new Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
            hostEnv.Setup(x => x.EnvironmentName).Returns("Production");

            _authManager = new AuthManager(
                _mockUserService.Object,
                _mockTenantService.Object,
                _mockTokenHelper.Object,
                _mockEvolutionApiService.Object,
                evoOptions.Object,
                _mockIyzicoPaymentService.Object,
                iyzicoOptions.Object,
                lockoutOptions.Object,
                new TenantAccessEvaluator(),
                _mockTenantPlanService.Object,
                hostEnv.Object,
                logger.Object
            );
        }

        // ─── 1. Trial Parmak İzi Testleri ────────────────────────────────────

        [Fact]
        public void ComputeTrialFingerprint_ShouldBeDeterministic()
        {
            // Aynı girdiler her zaman aynı hash'i üretmeli
            var fp1 = AuthManager.ComputeTrialFingerprint("5551112233", "Test Kuaför", "owner@test.com");
            var fp2 = AuthManager.ComputeTrialFingerprint("5551112233", "Test Kuaför", "owner@test.com");

            fp1.Should().Be(fp2);
            fp1.Should().NotBeNullOrEmpty();
            fp1.Length.Should().Be(64, "SHA256 hex string 64 karakter olmalı");
        }

        [Fact]
        public void ComputeTrialFingerprint_ShouldBeCaseInsensitive()
        {
            // Büyük/küçük harf farkı olmamalı
            var fp1 = AuthManager.ComputeTrialFingerprint("5551112233", "TEST KUAFÖR", "OWNER@TEST.COM");
            var fp2 = AuthManager.ComputeTrialFingerprint("5551112233", "test kuaför", "owner@test.com");

            fp1.Should().Be(fp2);
        }

        [Fact]
        public void ComputeTrialFingerprint_ShouldDifferForDifferentInputs()
        {
            var fp1 = AuthManager.ComputeTrialFingerprint("5551112233", "Kuaför A", "owner@a.com");
            var fp2 = AuthManager.ComputeTrialFingerprint("5552223344", "Kuaför B", "owner@b.com");

            fp1.Should().NotBe(fp2);
        }

        // ─── 2. Mükerrer Trial Engelleme ─────────────────────────────────────

        [Fact]
        public async Task RegisterBusinessOwnerAsync_ShouldBlock_WhenTrialAlreadyUsedByFingerprint()
        {
            // Arrange: Kullanıcı mevcut değil (yeni e-posta/telefon)
            _mockUserService.Setup(x => x.GetByMail(It.IsAny<string>())).ReturnsAsync((AppUser?)null);
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(It.IsAny<string>())).ReturnsAsync((AppUser?)null);

            // Aynı parmak iziyle daha önce trial kullanılmış (TrialUsed=true)
            var existingTenant = new Tenant { TenantID = 1, TrialUsed = true };
            _mockTenantService.Setup(x => x.GetByFingerprintAsync(It.IsAny<string>()))
                              .ReturnsAsync(existingTenant);

            var dto = new BusinessRegistrationDto
            {
                UserEmail = "attacker@test.com",
                PhoneNumber = "5551112233",
                BusinessName = "Sahte Kuaför",
                PlanType = "trial",
                BillingCycle = "Monthly"
            };

            // Act
            Func<Task> act = async () => await _authManager.RegisterBusinessOwnerAsync(dto);

            // Assert
            var exception = await act.Should().ThrowAsync<BadHttpRequestException>();
            exception.WithMessage("Bu işletme/numara için deneme süresi dolmuştur. Lütfen bir abonelik planı seçin.");

            // Tenant asla oluşturulmamalı
            _mockTenantService.Verify(x => x.AddTenantAsync(It.IsAny<TenantCreateDto>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RegisterBusinessOwnerAsync_ShouldAllow_WhenFingerprintExistsButTrialNotUsed()
        {
            // Edge case: Fingerprint eşleşiyor ama daha önce trial kullanılmamış
            _mockUserService.Setup(x => x.GetByMail(It.IsAny<string>())).ReturnsAsync((AppUser?)null);
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(It.IsAny<string>())).ReturnsAsync((AppUser?)null);

            // TrialUsed = false — geçmişte kayıt var ama trial kullanılmamış
            var existingTenant = new Tenant { TenantID = 1, TrialUsed = false };
            _mockTenantService.Setup(x => x.GetByFingerprintAsync(It.IsAny<string>()))
                              .ReturnsAsync(existingTenant);

            _mockTenantService.Setup(x => x.AddTenantAsync(It.IsAny<TenantCreateDto>(), It.IsAny<string>())).ReturnsAsync(99);
            _mockTenantService.Setup(x => x.GetByIdAsync(99)).ReturnsAsync(new Tenant
            {
                TenantID = 99,
                IsActive = true,
                IsSubscriptionActive = true,
                SubscriptionEndDate = DateTime.Now.AddDays(15)
            });
            _mockTenantService.Setup(x => x.UpdateAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);

            var dto = new BusinessRegistrationDto
            {
                UserEmail = "new@test.com",
                PhoneNumber = "5551112233",
                BusinessName = "Yeni Kuaför",
                PlanType = "trial",
                BillingCycle = "Monthly",
                SectorID = 1
            };

            Func<Task> act = async () => await _authManager.RegisterBusinessOwnerAsync(dto);

            await act.Should().NotThrowAsync();
            _mockTenantService.Verify(x => x.AddTenantAsync(It.IsAny<TenantCreateDto>(), It.IsAny<string>()), Times.Once);
            _mockUserService.Verify(x => x.AddAppUserAsync(It.IsAny<AppUser>()), Times.Never);
        }

        // ─── 3. Blacklist Giriş Engeli ────────────────────────────────────────

        [Fact]
        public async Task GenerateOtpForLoginAsync_ShouldBlockWithGenericMessage_WhenBlacklisted()
        {
            // Arrange
            var phone = OtpTestHelper.Normalize("5559876543");
            var dto = new OtpLoginDto { PhoneNumber = phone };
            var user = new AppUser { TenantID = 7 };
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(phone)).ReturnsAsync(user);

            var blacklistedTenant = new Tenant
            {
                TenantID = 7,
                IsBlacklisted = true,
                IsActive = false,
                IsSubscriptionActive = false,
                SubscriptionEndDate = DateTime.Now.AddDays(5)
            };
            _mockTenantService.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(blacklistedTenant);

            // Act
            Func<Task> act = async () => await _authManager.GenerateOtpForLoginAsync(dto);

            // Assert: Genel mesaj — blacklist detayı açıklanmamalı
            var exception = await act.Should().ThrowAsync<BadHttpRequestException>();
            exception.WithMessage("Hesabınız kullanıma kapatılmıştır.");

            _mockEvolutionApiService.Verify(
                x => x.SendOtpMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
            _mockUserService.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
        }

        [Fact]
        public async Task GenerateOtpForLoginAsync_ShouldThrowSpecific_WhenSubscriptionExpired()
        {
            var phone = OtpTestHelper.Normalize("5551112233");
            var dto = new OtpLoginDto { PhoneNumber = phone };
            var user = new AppUser { TenantID = 3 };
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(phone)).ReturnsAsync(user);

            var expiredTenant = new Tenant
            {
                TenantID = 3,
                IsBlacklisted = false,
                IsActive = true,
                IsSubscriptionActive = true,
                IsTrial = false,
                SubscriptionEndDate = DateTime.Now.AddDays(-5) // Süresi dolmuş
            };
            _mockTenantService.Setup(x => x.GetByIdAsync(3)).ReturnsAsync(expiredTenant);
            _mockTenantService.Setup(x => x.UpdateAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);

            Func<Task> act = async () => await _authManager.GenerateOtpForLoginAsync(dto);

            var exception = await act.Should().ThrowAsync<BadHttpRequestException>();
            exception.Which.Message.Should().Contain("aboneliği");
        }

        // ─── 4. TrialFingerprint Kaydı Kontrolü ─────────────────────────────

        [Fact]
        public async Task RegisterBusinessOwnerAsync_ShouldSetTrialUsed_WhenPlanIsTrial()
        {
            // Arrange
            _mockUserService.Setup(x => x.GetByMail(It.IsAny<string>())).ReturnsAsync((AppUser?)null);
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(It.IsAny<string>())).ReturnsAsync((AppUser?)null);
            _mockTenantService.Setup(x => x.GetByFingerprintAsync(It.IsAny<string>())).ReturnsAsync((Tenant?)null);
            _mockTenantService.Setup(x => x.AddTenantAsync(It.IsAny<TenantCreateDto>(), It.IsAny<string>())).ReturnsAsync(50);

            Tenant? capturedTenant = null;
            var returnedTenant = new Tenant { TenantID = 50, IsActive = true, IsSubscriptionActive = true };
            _mockTenantService.Setup(x => x.GetByIdAsync(50)).ReturnsAsync(returnedTenant);
            _mockTenantService.Setup(x => x.UpdateAsync(It.IsAny<Tenant>()))
                              .Callback<Tenant>(t => capturedTenant = t)
                              .Returns(Task.CompletedTask);
            _mockUserService.Setup(x => x.AddAppUserAsync(It.IsAny<AppUser>())).ReturnsAsync(1);
            _mockUserOpClaimService.Setup(x => x.AddAsync(It.IsAny<UserOperationClaim>())).Returns(Task.CompletedTask);

            var dto = new BusinessRegistrationDto
            {
                UserEmail = "brand@new.com",
                PhoneNumber = "5550000001",
                BusinessName = "İlk Trial Dükkan",
                PlanType = "trial",
                BillingCycle = "Monthly",
                SectorID = 1
            };

            // Act
            await _authManager.RegisterBusinessOwnerAsync(dto);

            // Assert: TrialUsed = true
            capturedTenant.Should().NotBeNull();
            capturedTenant!.TrialUsed.Should().BeTrue();
            _mockUserService.Verify(x => x.AddAppUserAsync(It.IsAny<AppUser>()), Times.Never);
        }

        [Fact]
        public async Task RegisterBusinessOwnerAsync_ShouldNotSetTrialUsed_WhenPlanIsPaid()
        {
            _mockUserService.Setup(x => x.GetByMail(It.IsAny<string>())).ReturnsAsync((AppUser?)null);
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(It.IsAny<string>())).ReturnsAsync((AppUser?)null);
            _mockTenantService.Setup(x => x.AddTenantAsync(It.IsAny<TenantCreateDto>(), It.IsAny<string>())).ReturnsAsync(51);

            Tenant? capturedTenant = null;
            var returnedTenant = new Tenant { TenantID = 51, IsActive = true, IsSubscriptionActive = true };
            _mockTenantService.Setup(x => x.GetByIdAsync(51)).ReturnsAsync(returnedTenant);
            _mockTenantService.Setup(x => x.UpdateAsync(It.IsAny<Tenant>()))
                              .Callback<Tenant>(t => capturedTenant = t)
                              .Returns(Task.CompletedTask);
            _mockUserService.Setup(x => x.AddAppUserAsync(It.IsAny<AppUser>())).ReturnsAsync(1);
            _mockUserOpClaimService.Setup(x => x.AddAsync(It.IsAny<UserOperationClaim>())).Returns(Task.CompletedTask);

            var dto = new BusinessRegistrationDto
            {
                UserEmail = "paid@user.com",
                PhoneNumber = "5550000002",
                BusinessName = "Ücretli Dükkan",
                PlanType = "Starter",
                BillingCycle = "Monthly",
                SectorID = 1
            };

            await _authManager.RegisterBusinessOwnerAsync(dto);

            capturedTenant.Should().NotBeNull();
            capturedTenant!.TrialUsed.Should().BeFalse("Ücretli plan seçildiğinde TrialUsed true olmamalı");
            _mockUserService.Verify(x => x.AddAppUserAsync(It.IsAny<AppUser>()), Times.Never);
        }
    }
}
