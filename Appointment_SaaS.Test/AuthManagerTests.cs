using System;
using System.Threading.Tasks;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using Appointment_SaaS.Core.Utilities.Security.JWT;
using Appointment_SaaS.Core.Utilities.Security;
using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Appointment_SaaS.Core.Utilities.Security.Jwt;

namespace Appointment_SaaS.Test
{
    public class AuthManagerTests
    {
        private readonly Mock<IAppUserService> _mockUserService;
        private readonly Mock<ITenantService> _mockTenantService;
        private readonly Mock<ITokenHelper> _mockTokenHelper;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IOtpService> _mockOtpService;
        private readonly Mock<IEvolutionApiService> _mockEvolutionApiService;
        private readonly Mock<IUserOperationClaimService> _mockUserOpClaimService;
        private readonly Mock<IOptions<EvolutionApiSettings>> _mockEvoOptions;
        private readonly Mock<IIyzicoPaymentService> _mockIyzicoPaymentService;
        private readonly Mock<IOptions<IyzicoSettings>> _mockIyzicoOptions;

        private readonly AuthManager _authManager;

        public AuthManagerTests()
        {
            _mockUserService = new Mock<IAppUserService>();
            _mockTenantService = new Mock<ITenantService>();
            _mockTokenHelper = new Mock<ITokenHelper>();
            _mockMapper = new Mock<IMapper>();
            _mockOtpService = new Mock<IOtpService>();
            _mockEvolutionApiService = new Mock<IEvolutionApiService>();
            _mockUserOpClaimService = new Mock<IUserOperationClaimService>();

            _mockEvoOptions = new Mock<IOptions<EvolutionApiSettings>>();
            _mockEvoOptions.Setup(x => x.Value).Returns(new EvolutionApiSettings { DefaultInstance = "defaultConfig" });

            _mockIyzicoOptions = new Mock<IOptions<IyzicoSettings>>();
            _mockIyzicoOptions.Setup(x => x.Value).Returns(new IyzicoSettings { Enabled = true });

            _mockIyzicoPaymentService = new Mock<IIyzicoPaymentService>();

            var lockoutOptions = new Mock<IOptions<LockoutSettings>>();
            lockoutOptions.Setup(x => x.Value).Returns(new LockoutSettings
            {
                MaxFailedAccessAttempts = 5,
                DefaultLockoutTimeSpanInMinutes = 10
            });

            var logger = new Mock<ILogger<AuthManager>>();

            _authManager = new AuthManager(
                _mockUserService.Object,
                _mockTenantService.Object,
                _mockTokenHelper.Object,
                _mockMapper.Object,
                _mockOtpService.Object,
                _mockEvolutionApiService.Object,
                _mockUserOpClaimService.Object,
                _mockEvoOptions.Object,
                _mockIyzicoPaymentService.Object,
                _mockIyzicoOptions.Object,
                lockoutOptions.Object,
                logger.Object
            );
        }

        [Fact]
        public async Task RegisterBusinessOwnerAsync_ShouldThrowException_WhenUserOrPhoneExists()
        {
            var dto = new BusinessRegistrationDto { UserEmail = "test@test.com", PhoneNumber = "5551112233" };
            _mockUserService.Setup(x => x.GetByMail(dto.UserEmail)).ReturnsAsync(new AppUser());
            Func<Task> act = async () => await _authManager.RegisterBusinessOwnerAsync(dto);
            var exception = await act.Should().ThrowAsync<BadHttpRequestException>();
            exception.WithMessage("Bu e-posta veya telefon numarası zaten kullanımda.");
            _mockTenantService.Verify(x => x.AddTenantAsync(It.IsAny<TenantCreateDto>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RegisterBusinessOwnerAsync_ShouldThrowException_WhenCardInfoMissing()
        {
            // Kart bilgisi boş — artık kart ZORUNLU
            var dto = new BusinessRegistrationDto
            {
                UserEmail = "test@firma.com",
                PhoneNumber = "5551112233",
                UserFullName = "Test Kullanıcı",
                BusinessName = "Test İşletme",
                SectorID = 1,
                CardHolderName = "", // BOŞ!
                CardNumber = "",
                ExpireMonth = "",
                ExpireYear = "",
                Cvc = ""
            };

            _mockUserService.Setup(x => x.GetByMail(dto.UserEmail)).ReturnsAsync((AppUser?)null);
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(dto.PhoneNumber)).ReturnsAsync((AppUser?)null);
            _mockTenantService.Setup(x => x.GetByFingerprintAsync(It.IsAny<string>())).ReturnsAsync((Tenant?)null);
            _mockTenantService.Setup(x => x.AddTenantAsync(It.IsAny<TenantCreateDto>(), It.IsAny<string>())).ReturnsAsync(1);
            _mockTenantService.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(new Tenant { TenantID = 1 });

            Func<Task> act = async () => await _authManager.RegisterBusinessOwnerAsync(dto);
            (await act.Should().ThrowAsync<BadHttpRequestException>())
                .WithMessage("Kart bilgileri zorunludur. Lütfen tüm alanları eksiksiz doldurunuz.");
        }

        [Fact]
        public async Task GenerateOtpForLoginAsync_ShouldThrowException_WhenSpamLimitHit()
        {
            var dto = new OtpLoginDto { PhoneNumber = "555" };
            var user = new AppUser { TenantID = 1, LastOtpRequestDate = DateTime.Now.AddSeconds(-30) }; // 30 sn once istemis
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(dto.PhoneNumber)).ReturnsAsync(user);

            var tenant = new Tenant { TenantID = 1, IsActive = true, IsSubscriptionActive = true, SubscriptionEndDate = DateTime.Now.AddDays(10) };
            _mockTenantService.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(tenant);

            Func<Task> act = async () => await _authManager.GenerateOtpForLoginAsync(dto);
            (await act.Should().ThrowAsync<BadHttpRequestException>()).WithMessage("Lütfen yeni bir kod istemeden önce 45 saniye bekleyin.");
        }

        [Fact]
        public async Task GenerateOtpForLoginAsync_ShouldThrowException_WhenTenantIsPassive()
        {
            var dto = new OtpLoginDto { PhoneNumber = "5551234567" };
            var user = new AppUser { TenantID = 1, LastOtpRequestDate = DateTime.Now.AddMinutes(-5) };
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(dto.PhoneNumber)).ReturnsAsync(user);

            // İşletme PASİF
            var tenant = new Tenant { TenantID = 1, IsActive = false, IsSubscriptionActive = true, SubscriptionEndDate = DateTime.Now.AddDays(10) };
            _mockTenantService.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(tenant);

            Func<Task> act = async () => await _authManager.GenerateOtpForLoginAsync(dto);
            (await act.Should().ThrowAsync<BadHttpRequestException>())
                .WithMessage("Hesabınız askıya alınmıştır. Lütfen yöneticiyle iletişime geçin.");
        }

        [Fact]
        public async Task VerifyOtpAndLoginAsync_ShouldThrowException_WhenTenantIsPassive()
        {
            var dto = new OtpVerifyDto { PhoneNumber = "5551234567", OtpCode = "123456" };
            var user = new AppUser { TenantID = 1, OtpCode = "123456", OtpExpiry = DateTime.Now.AddMinutes(1) };
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(dto.PhoneNumber)).ReturnsAsync(user);

            // İşletme PASİF
            var tenant = new Tenant { TenantID = 1, IsActive = false, IsSubscriptionActive = true, SubscriptionEndDate = DateTime.Now.AddDays(10) };
            _mockTenantService.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(tenant);

            Func<Task> act = async () => await _authManager.VerifyOtpAndLoginAsync(dto);
            (await act.Should().ThrowAsync<BadHttpRequestException>())
                .WithMessage("Hesabınız askıya alınmıştır. Lütfen yöneticiyle iletişime geçin.");
        }

        [Fact]
        public async Task GenerateOtpForLoginAsync_ShouldUpdateUserAndSendSms_WhenValid()
        {
            var dto = new OtpLoginDto { PhoneNumber = "555" };
            var user = new AppUser { TenantID = 1, LastOtpRequestDate = DateTime.Now.AddMinutes(-5) }; // 5 dk gecmis (Uygun)
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(dto.PhoneNumber)).ReturnsAsync(user);

            var tenant = new Tenant { TenantID = 1, InstanceName = "TestInstance", IsActive = true, IsSubscriptionActive = true, SubscriptionEndDate = DateTime.Now.AddDays(10) };
            _mockTenantService.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(tenant);
            _mockTenantService.Setup(x => x.GetByPhoneNumberAsync(dto.PhoneNumber)).ReturnsAsync(tenant);
            
            _mockEvolutionApiService.Setup(x => x.SendOtpMessageAsync("TestInstance", dto.PhoneNumber, It.IsAny<string>())).ReturnsAsync(true);

            var result = await _authManager.GenerateOtpForLoginAsync(dto);

            result.Should().BeTrue();
            user.OtpCode.Should().NotBeNullOrEmpty();
            user.OtpExpiry.Should().BeAfter(DateTime.Now);
            _mockUserService.Verify(x => x.UpdateAsync(user), Times.Once);
        }

        [Fact]
        public async Task VerifyOtpAndLoginAsync_ShouldThrowException_WhenOtpIsWrong()
        {
            var dto = new OtpVerifyDto { PhoneNumber = "555", OtpCode = "000000" };
            var user = new AppUser { TenantID = 1, OtpCode = "123456", OtpExpiry = DateTime.Now.AddMinutes(1), TrialEndDate = DateTime.Now.AddDays(10) };
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(dto.PhoneNumber)).ReturnsAsync(user);

            // Tenant kontrolü OTP'den önce yapılıyor — aktif tenant mock lazım
            var tenant = new Tenant { TenantID = 1, IsActive = true, IsSubscriptionActive = true, SubscriptionEndDate = DateTime.Now.AddDays(10) };
            _mockTenantService.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(tenant);

            Func<Task> act = async () => await _authManager.VerifyOtpAndLoginAsync(dto);
            (await act.Should().ThrowAsync<BadHttpRequestException>()).WithMessage("Hatalı veya süresi geçmiş kod.");
        }

        [Fact]
        public async Task VerifyOtpAndLoginAsync_ShouldThrowException_WhenOtpIsExpired()
        {
            var dto = new OtpVerifyDto { PhoneNumber = "555", OtpCode = "123456" };
            var user = new AppUser { TenantID = 1, OtpCode = "123456", OtpExpiry = DateTime.Now.AddMinutes(-1), TrialEndDate = DateTime.Now.AddDays(10) }; // Suresi dolmus
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(dto.PhoneNumber)).ReturnsAsync(user);

            // Tenant kontrolü OTP'den önce yapılıyor — aktif tenant mock lazım
            var tenant = new Tenant { TenantID = 1, IsActive = true, IsSubscriptionActive = true, SubscriptionEndDate = DateTime.Now.AddDays(10) };
            _mockTenantService.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(tenant);

            Func<Task> act = async () => await _authManager.VerifyOtpAndLoginAsync(dto);
            (await act.Should().ThrowAsync<BadHttpRequestException>()).WithMessage("Hatalı veya süresi geçmiş kod.");
        }

        [Fact]
        public async Task VerifyOtpAndLoginAsync_ShouldReturnToken_WhenSuccess()
        {
            var dto = new OtpVerifyDto { PhoneNumber = "555", OtpCode = "123456" };
            var user = new AppUser { AppUserID = 99, TenantID = 1, OtpCode = "123456", OtpExpiry = DateTime.Now.AddMinutes(2), TrialEndDate = DateTime.Now.AddDays(1) };
            _mockUserService.Setup(x => x.GetByPhoneNumberAsync(dto.PhoneNumber)).ReturnsAsync(user);

            var tenant = new Tenant { TenantID = 1, IsActive = true, IsSubscriptionActive = true, SubscriptionEndDate = DateTime.Now.AddDays(1), IsTrial = true };
            _mockTenantService.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(tenant);

            var claims = new System.Collections.Generic.List<OperationClaim>();
            _mockUserService.Setup(x => x.GetClaims(user)).Returns(claims);
            
            var expectedToken = new AccessToken { Token = "token", Expiration = DateTime.Now.AddHours(2) };
            _mockTokenHelper.Setup(x => x.CreateToken(user, claims)).ReturnsAsync(expectedToken);

            var result = await _authManager.VerifyOtpAndLoginAsync(dto);

            result.Should().BeEquivalentTo(expectedToken);
            user.OtpCode.Should().BeNull(); // Used token must be reset
            user.OtpExpiry.Should().BeNull();
            _mockUserService.Verify(x => x.UpdateAsync(user), Times.Once);
        }
    }
}

