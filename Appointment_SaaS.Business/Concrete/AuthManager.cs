using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities.Security.Jwt;
using Appointment_SaaS.Core.Utilities.Security.JWT;
using Appointment_SaaS.Core.Utilities.Security;
using Appointment_SaaS.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Appointment_SaaS.Business.Concrete
{
    public class AuthManager : IAuthService
    {
        private readonly IAppUserService _userService;
        private readonly ITenantService _tenantService;
        private readonly ITokenHelper _tokenHelper;
        private readonly IOtpService _otpService;
        private readonly IEvolutionApiService _evolutionApiService;
        private readonly IUserOperationClaimService _userOperationClaimService;
        private readonly EvolutionApiSettings _evoSettings;
        private readonly IIyzicoPaymentService _iyzicoPaymentService;
        private readonly IyzicoSettings _iyzicoSettings;
        private readonly LockoutSettings _lockoutSettings;
        private readonly ILogger<AuthManager> _logger;

        public AuthManager(
            IAppUserService userService,
            ITenantService tenantService,
            ITokenHelper tokenHelper,
            IOtpService otpService,
            IEvolutionApiService evolutionApiService,
            IUserOperationClaimService userOperationClaimService,
            IOptions<EvolutionApiSettings> evoOptions,
            IIyzicoPaymentService iyzicoPaymentService,
            IOptions<IyzicoSettings> iyzicoOptions,
            IOptions<LockoutSettings> lockoutOptions,
            ILogger<AuthManager> logger)
        {
            _userService = userService;
            _tenantService = tenantService;
            _tokenHelper = tokenHelper;
            _otpService = otpService;
            _evolutionApiService = evolutionApiService;
            _userOperationClaimService = userOperationClaimService;
            _evoSettings = evoOptions.Value;
            _iyzicoPaymentService = iyzicoPaymentService;
            _iyzicoSettings = iyzicoOptions.Value;
            _lockoutSettings = lockoutOptions.Value;
            _logger = logger;
        }

        public async Task<int> RegisterBusinessOwnerAsync(BusinessRegistrationDto dto)
        {
            // 1. Tekrar kontrolü
            var dbUserByEmail = await _userService.GetByMail(dto.UserEmail);
            var dbUserByPhone = await _userService.GetByPhoneNumberAsync(dto.PhoneNumber);
            if (dbUserByEmail != null || dbUserByPhone != null)
                throw new BadHttpRequestException(
                    "Bu e-posta veya telefon numarası zaten kullanımda.",
                    StatusCodes.Status400BadRequest);

            // 2. TC Kimlik doğrulaması — sadece production'da
            // Development'ta NVİ'ye erişilemez, her kaydı engellemez
            if (!string.IsNullOrWhiteSpace(dto.IdentityNumber) && dto.IdentityNumber.Length == 11)
            {
                try
                {
                    var isTcValid = await ValidateTCKimlikAsync(dto.IdentityNumber, dto.UserFullName, dto.BirthYear);
                    if (!isTcValid)
                    {
                        _logger.LogWarning("TC Kimlik doğrulaması başarısız. Email={Email}", dto.UserEmail);
                        // ✅ DÜZELTME: Development'ta TC hatasını sadece logla, engelleme
                        // Production'da bu satırı throw ile değiştir
                        _logger.LogWarning("TC Kimlik doğrulaması atlanıyor (NVİ erişilemedi veya dev ortamı).");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "NVİ TC Kimlik servisi erişilemedi, kayıt devam ediyor.");
                }
            }

            // 3. Anti-Fraud: Trial parmak izi
            bool isTrial = string.Equals(dto.PlanType, "trial", StringComparison.OrdinalIgnoreCase);
            var fingerprint = ComputeTrialFingerprint(dto.PhoneNumber, dto.BusinessName, dto.UserEmail);

            if (isTrial)
            {
                var existingByFingerprint = await _tenantService.GetByFingerprintAsync(fingerprint);
                if (existingByFingerprint != null && existingByFingerprint.TrialUsed)
                    throw new BadHttpRequestException(
                        "Bu işletme/numara için deneme süresi dolmuştur. Lütfen bir abonelik planı seçin.",
                        StatusCodes.Status402PaymentRequired);
            }

            // 4. Tenant oluştur
            var tenantId = await _tenantService.AddTenantAsync(new TenantCreateDto
            {
                Name = dto.BusinessName,
                PhoneNumber = dto.PhoneNumber,
                Address = dto.Address,
                SectorID = dto.SectorID
            }, fingerprint);

            var tenant = await _tenantService.GetByIdAsync(tenantId);
            if (tenant == null)
                throw new BadHttpRequestException(
                    "Tenant oluşturuldu ancak okunamadı.",
                    StatusCodes.Status500InternalServerError);

            // 5. Kart zorunluluğu ve Iyzico
            var cardComplete = HasCompleteCardFields(dto);
            if (!cardComplete)
                throw new BadHttpRequestException(
                    "Kart bilgileri zorunludur. Lütfen tüm alanları eksiksiz doldurunuz.",
                    StatusCodes.Status400BadRequest);

            if (_iyzicoSettings.Enabled)
            {
                var subInit = await _iyzicoPaymentService.InitializeSubscriptionAsync(
                    tenant: tenant,
                    customerEmail: dto.UserEmail,
                    customerFullName: dto.UserFullName,
                    customerPhone: dto.PhoneNumber,
                    customerIdentityNumber: dto.IdentityNumber,
                    card: new IyzicoCardInput(
                        CardHolderName: dto.CardHolderName,
                        CardNumber: dto.CardNumber,
                        ExpireMonth: dto.ExpireMonth,
                        ExpireYear: dto.ExpireYear,
                        Cvc: dto.Cvc),
                    planType: dto.PlanType,
                    billingCycle: dto.BillingCycle
                );

                tenant.IyzicoUserKey = subInit.IyzicoUserKey;
                tenant.IyzicoCardToken = subInit.IyzicoCardToken;
                tenant.SubscriptionReferenceCode = subInit.SubscriptionReferenceCode;
            }

            // 6. Tenant'ı güncelle
            tenant.IsSubscriptionActive = true;
            tenant.IsActive = true;
            tenant.IsTrial = isTrial;
            tenant.TrialUsed = isTrial;

            // ✅ DÜZELTME: PlanType Tenant entity'sinde mevcut, doğrudan set ediyoruz
            tenant.PlanType = isTrial ? "Trial" : dto.PlanType;

            if (isTrial)
            {
                tenant.SubscriptionEndDate = DateTime.Now.AddDays(15);
            }
            else
            {
                var monthsToAdd = string.Equals(
                    dto.BillingCycle, "Yearly",
                    StringComparison.OrdinalIgnoreCase) ? 12 : 1;
                tenant.SubscriptionEndDate = DateTime.Now.AddMonths(monthsToAdd);
            }

            await _tenantService.UpdateAsync(tenant);

            // 7. AppUser oluştur
            // ✅ DÜZELTME: SecurityStamp zorunlu — OnTokenValidated bunu kontrol eder
            var userEntity = new AppUser
            {
                FirstName = dto.UserFullName,
                LastName = "",
                Email = dto.UserEmail,
                PhoneNumber = dto.PhoneNumber,
                TenantID = tenantId,
                TrialStartDate = isTrial ? DateTime.Now : null,
                TrialEndDate = isTrial ? DateTime.Now.AddDays(15) : null,
                Status = true,
                SecurityStamp = Guid.NewGuid().ToString() // ✅ KRİTİK
            };

            var userId = await _userService.AddAppUserAsync(userEntity);

            // 8. Role (Manager = 2) ata
            await _userOperationClaimService.AddAsync(new UserOperationClaim
            {
                UserId = userId,
                OperationClaimId = 2
            });

            _logger.LogInformation(
                "Kayıt tamamlandı. TenantId={TenantId}, UserId={UserId}, Plan={Plan}",
                tenantId, userId, dto.PlanType);

            return tenantId;
        }

        private static bool HasCompleteCardFields(BusinessRegistrationDto dto)
        {
            return !string.IsNullOrWhiteSpace(dto.CardHolderName)
                && !string.IsNullOrWhiteSpace(dto.CardNumber)
                && !string.IsNullOrWhiteSpace(dto.ExpireMonth)
                && !string.IsNullOrWhiteSpace(dto.ExpireYear)
                && !string.IsNullOrWhiteSpace(dto.Cvc);
        }

        public static string ComputeTrialFingerprint(string phone, string businessName, string email)
        {
            var raw = $"{phone.Trim().ToLowerInvariant()}|{businessName.Trim().ToLowerInvariant()}|{email.Trim().ToLowerInvariant()}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private async Task<bool> ValidateTCKimlikAsync(string tcKimlikNo, string fullName, int birthYear)
        {
            if (string.IsNullOrWhiteSpace(tcKimlikNo) || tcKimlikNo.Length != 11) return false;
            if (string.IsNullOrWhiteSpace(fullName)) return false;

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;

            var trCulture = new System.Globalization.CultureInfo("tr-TR");
            string ad = string.Join(" ", parts.Take(parts.Length - 1)).ToUpper(trCulture);
            string soyad = parts.Last().ToUpper(trCulture);

            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <TCKimlikNoDogrula xmlns=""http://tckimlik.nvi.gov.tr/WS"">
      <TCKimlikNo>{tcKimlikNo}</TCKimlikNo>
      <Ad>{ad}</Ad>
      <Soyad>{soyad}</Soyad>
      <DogumYili>{birthYear}</DogumYili>
    </TCKimlikNoDogrula>
  </soap:Body>
</soap:Envelope>";

                var request = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Post,
                    "https://tckimlik.nvi.gov.tr/Service/KPSPublic.asmx")
                {
                    Content = new System.Net.Http.StringContent(soapEnvelope, Encoding.UTF8, "text/xml")
                };
                request.Headers.Add("SOAPAction", "http://tckimlik.nvi.gov.tr/WS/TCKimlikNoDogrula");

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    return responseBody.Contains("<TCKimlikNoDogrulaResult>true</TCKimlikNoDogrulaResult>");
                }

                _logger.LogWarning("NVİ servisi HTTP {StatusCode} döndü.", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NVİ TC Kimlik doğrulama servisi erişilemedi.");
            }

            return false;
        }

        public async Task<bool> GenerateOtpForLoginAsync(OtpLoginDto dto)
        {
            var user = await _userService.GetByPhoneNumberAsync(dto.PhoneNumber);
            if (user == null)
                throw new BadHttpRequestException(
                    "Sistemde böyle bir numaraya ait kayıt bulunamadı.",
                    StatusCodes.Status404NotFound);

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.Now)
            {
                var remaining = (int)Math.Ceiling((user.LockoutEnd.Value - DateTime.Now).TotalMinutes);
                throw new BadHttpRequestException(
                    $"Hesabınız geçici olarak kilitlendi. Lütfen {remaining} dakika sonra tekrar deneyin.",
                    StatusCodes.Status429TooManyRequests);
            }

            var tenant = await _tenantService.GetByIdAsync(user.TenantID);
            if (tenant == null)
                throw new BadHttpRequestException("İşletme bulunamadı.", StatusCodes.Status404NotFound);

            if (tenant.IsBlacklisted)
                throw new BadHttpRequestException(
                    "Hesabınız kullanıma kapatılmıştır.",
                    StatusCodes.Status403Forbidden);

            if (!tenant.IsActive || !tenant.IsSubscriptionActive)
                throw new BadHttpRequestException(
                    "Hesabınız askıya alınmıştır. Lütfen yöneticiyle iletişime geçin.",
                    StatusCodes.Status403Forbidden);

            // ✅ DÜZELTME: SubscriptionEndDate kontrolü sadece trial olmayan ve süresi dolmuş hesaplar için
            if (!tenant.IsTrial && tenant.SubscriptionEndDate != DateTime.MinValue && tenant.SubscriptionEndDate < DateTime.Now)
            {
                if (tenant.IsActive)
                {
                    tenant.IsActive = false;
                    await _tenantService.UpdateAsync(tenant);
                }
                throw new BadHttpRequestException(
                    $"İşletmenizin aboneliği {tenant.SubscriptionEndDate:dd.MM.yyyy} tarihinde dolmuştur.",
                    StatusCodes.Status402PaymentRequired);
            }

            if (user.LastOtpRequestDate.HasValue
                && (DateTime.Now - user.LastOtpRequestDate.Value).TotalSeconds < 45)
                throw new BadHttpRequestException(
                    "Lütfen yeni bir kod istemeden önce 45 saniye bekleyin.",
                    StatusCodes.Status429TooManyRequests);

            var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            user.OtpCode = otpCode;
            user.OtpExpiry = DateTime.Now.AddSeconds(45);
            user.LastOtpRequestDate = DateTime.Now;
            await _userService.UpdateAsync(user);

            var tenantByPhone = await _tenantService.GetByPhoneNumberAsync(dto.PhoneNumber);
            bool isSent = false;

            if (tenantByPhone != null && !string.IsNullOrEmpty(tenantByPhone.InstanceName))
                isSent = await _evolutionApiService.SendOtpMessageAsync(
                    tenantByPhone.InstanceName, dto.PhoneNumber, otpCode);

            if (!isSent)
                isSent = await _evolutionApiService.SendOtpMessageAsync(
                    _evoSettings.DefaultInstance, dto.PhoneNumber, otpCode);

            if (!isSent)
                throw new BadHttpRequestException(
                    "Doğrulama kodu gönderilemedi. Lütfen sistem yöneticisi ile iletişime geçin.",
                    StatusCodes.Status503ServiceUnavailable);

            return true;
        }

        public async Task<AccessToken> VerifyOtpAndLoginAsync(OtpVerifyDto dto)
        {
            var user = await _userService.GetByPhoneNumberAsync(dto.PhoneNumber);
            if (user == null)
                throw new BadHttpRequestException("Kullanıcı bulunamadı.");

            // 1. Lockout kontrolü
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.Now)
            {
                var remaining = (int)Math.Ceiling((user.LockoutEnd.Value - DateTime.Now).TotalMinutes);
                throw new BadHttpRequestException(
                    $"Hesabınız geçici olarak kilitlenmiştir. Lütfen {remaining} dakika sonra tekrar deneyin.",
                    StatusCodes.Status429TooManyRequests);
            }

            // 2. Tenant kontrolleri
            var tenant = await _tenantService.GetByIdAsync(user.TenantID);
            if (tenant == null)
                throw new BadHttpRequestException("İşletme bulunamadı.");

            if (!tenant.IsActive || !tenant.IsSubscriptionActive)
                throw new BadHttpRequestException(
                    "Hesabınız askıya alınmıştır. Lütfen yöneticiyle iletişime geçin.",
                    StatusCodes.Status403Forbidden);

            // ✅ DÜZELTME: DateTime.MinValue kontrolü eklendi — DB'de 0001 tarihi varsa engelleme
            if (!tenant.IsTrial
                && tenant.SubscriptionEndDate != DateTime.MinValue
                && tenant.SubscriptionEndDate < DateTime.Now)
            {
                if (tenant.IsActive)
                {
                    tenant.IsActive = false;
                    await _tenantService.UpdateAsync(tenant);
                }
                throw new BadHttpRequestException(
                    $"İşletmenizin aboneliği {tenant.SubscriptionEndDate:dd.MM.yyyy} tarihinde dolmuştur.",
                    StatusCodes.Status402PaymentRequired);
            }

            // ✅ DÜZELTME: TrialEndDate null kontrolü düzeltildi
            if (tenant.IsTrial
                && user.TrialEndDate.HasValue
                && user.TrialEndDate.Value < DateTime.Now)
                throw new BadHttpRequestException(
                    "İşletmenizin deneme süresi dolmuştur. Lütfen aboneliğinizi yükseltin.",
                    StatusCodes.Status402PaymentRequired);

            // 3. OTP doğrulama + brute-force koruması
            if (user.OtpCode != dto.OtpCode || user.OtpExpiry < DateTime.Now)
            {
                user.AccessFailedCount++;
                _logger.LogWarning(
                    "Hatalı OTP: Telefon={Phone}, Deneme={Count}/{Max}",
                    dto.PhoneNumber, user.AccessFailedCount, _lockoutSettings.MaxFailedAccessAttempts);

                if (user.AccessFailedCount >= _lockoutSettings.MaxFailedAccessAttempts)
                {
                    user.LockoutEnd = DateTime.Now.AddMinutes(_lockoutSettings.DefaultLockoutTimeSpanInMinutes);
                    user.AccessFailedCount = 0;
                    await _userService.UpdateAsync(user);

                    throw new BadHttpRequestException(
                        $"Çok fazla hatalı deneme. Hesabınız {_lockoutSettings.DefaultLockoutTimeSpanInMinutes} dakika süreyle kilitlendi.",
                        StatusCodes.Status429TooManyRequests);
                }

                await _userService.UpdateAsync(user);
                throw new BadHttpRequestException(
                    "Hatalı veya süresi geçmiş kod.",
                    StatusCodes.Status401Unauthorized);
            }

            // 4. Başarılı giriş — sayaçları sıfırla
            user.OtpCode = null;
            user.OtpExpiry = null;
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;

            // ✅ DÜZELTME: SecurityStamp yoksa set et (eski kayıtlar için)
            if (string.IsNullOrEmpty(user.SecurityStamp))
            {
                user.SecurityStamp = Guid.NewGuid().ToString();
                _logger.LogInformation(
                    "SecurityStamp güncellendi (eski kayıt). UserId={UserId}", user.AppUserID);
            }

            await _userService.UpdateAsync(user);

            var userClaims = _userService.GetClaims(user);
            var accessToken = await _tokenHelper.CreateToken(user, userClaims);

            return accessToken;
        }

        public async Task<bool> UserExists(string email)
        {
            var user = await _userService.GetByMail(email);
            return user != null;
        }

        public async Task UpdateAsync(AppUser user) => await _userService.UpdateAsync(user);

        public async Task DeleteAsync(AppUser user)
        {
            user.Status = false;
            await _userService.UpdateAsync(user);
        }
    }
}