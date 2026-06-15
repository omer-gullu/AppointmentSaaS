using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Diagnostics;
using Appointment_SaaS.Core.Constants;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities.Security.Jwt;
using Appointment_SaaS.Core.Utilities.Security.JWT;
using Appointment_SaaS.Core.Utilities.Security;
using Appointment_SaaS.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Linq;
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
        private readonly IEvolutionApiService _evolutionApiService;
        private readonly EvolutionApiSettings _evoSettings;
        private readonly IIyzicoPaymentService _iyzicoPaymentService;
        private readonly IyzicoSettings _iyzicoSettings;
        private readonly LockoutSettings _lockoutSettings;
        private readonly ITenantAccessEvaluator _tenantAccessEvaluator;
        private readonly ITenantPlanService _tenantPlanService;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ILogger<AuthManager> _logger;

        public AuthManager(
            IAppUserService userService,
            ITenantService tenantService,
            ITokenHelper tokenHelper,
            IEvolutionApiService evolutionApiService,
            IOptions<EvolutionApiSettings> evoOptions,
            IIyzicoPaymentService iyzicoPaymentService,
            IOptions<IyzicoSettings> iyzicoOptions,
            IOptions<LockoutSettings> lockoutOptions,
            ITenantAccessEvaluator tenantAccessEvaluator,
            ITenantPlanService tenantPlanService,
            IHostEnvironment hostEnvironment,
            ILogger<AuthManager> logger)
        {
            _userService = userService;
            _tenantService = tenantService;
            _tokenHelper = tokenHelper;
            _evolutionApiService = evolutionApiService;
            _evoSettings = evoOptions.Value;
            _iyzicoPaymentService = iyzicoPaymentService;
            _iyzicoSettings = iyzicoOptions.Value;
            _lockoutSettings = lockoutOptions.Value;
            _tenantAccessEvaluator = tenantAccessEvaluator;
            _tenantPlanService = tenantPlanService;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
        }

        private static string NormalizeOtpCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            var digits = new string(code.Where(char.IsDigit).ToArray());
            if (digits.Length >= 6) return digits[^6..];
            return digits.PadLeft(6, '0');
        }

        public async Task<(int TenantId, string CheckoutFormScript)> RegisterBusinessOwnerAsync(BusinessRegistrationDto dto, string callbackUrl = "")
        {
            try
            {
            // 1. Tekrar kontrolü (e-posta boşsa DB'de boş e-posta ile yanlış eşleşme olmasın)
            var dbUserByEmail = string.IsNullOrWhiteSpace(dto.UserEmail)
                ? null
                : await _userService.GetByMail(dto.UserEmail);
            var dbUserByPhone = await _userService.GetByPhoneNumberAsync(dto.PhoneNumber);

            if (dbUserByEmail != null && dbUserByPhone != null)
                throw new BadHttpRequestException(
                    "Bu e-posta adresi ve bu numara zaten kullanımda.",
                    StatusCodes.Status400BadRequest);
            if (dbUserByEmail != null)
                throw new BadHttpRequestException(
                    "Bu e-posta adresi zaten kullanımda.",
                    StatusCodes.Status400BadRequest);
            if (dbUserByPhone != null)
                throw new BadHttpRequestException(
                    "Bu numara zaten kullanımda.",
                    StatusCodes.Status400BadRequest);

            // 2. TC Kimlik doğrulaması — yalnızca production (NVİ gecikmesi kayıt timeout'una yol açmasın)
            dto.IdentityNumber = TurkishIdentityValidator.NormalizeIdentityNumber(dto.IdentityNumber);
            dto.UserFullName = TurkishTextNormalizer.ToTurkishTitleCase(dto.UserFullName);

            if (!_hostEnvironment.IsDevelopment()
                && !string.IsNullOrWhiteSpace(dto.IdentityNumber)
                && dto.IdentityNumber.Length == 11)
            {
                if (!TurkishIdentityValidator.IsValidTcKimlik(dto.IdentityNumber))
                {
                    throw new BadHttpRequestException(
                        "Girdiğiniz T.C. kimlik numarası geçersiz (kontrol basamağı hatası).",
                        StatusCodes.Status400BadRequest);
                }

                var nameParts = TurkishTextNormalizer.SplitTurkishFullName(dto.UserFullName);
                if (nameParts == null)
                {
                    throw new BadHttpRequestException(
                        "Ad ve soyadınızı kimliğinizdeki gibi, en az iki kelime olacak şekilde giriniz (ör. Ahmet Yılmaz).",
                        StatusCodes.Status400BadRequest);
                }

                if (dto.BirthYear < 1900 || dto.BirthYear > DateTime.UtcNow.Year - 18)
                {
                    throw new BadHttpRequestException(
                        "Doğum yılınızı kimliğinizdeki bilgiyle birebir giriniz.",
                        StatusCodes.Status400BadRequest);
                }

                try
                {
                    var isTcValid = await ValidateTCKimlikAsync(
                        dto.IdentityNumber,
                        nameParts.Value.Ad,
                        nameParts.Value.Soyad,
                        dto.BirthYear);
                    if (!isTcValid)
                    {
                        throw new BadHttpRequestException(
                            "T.C. kimlik numarası, ad-soyad veya doğum yılı NVİ kayıtlarıyla eşleşmiyor. Bilgilerinizi kimliğinizdeki gibi birebir girin.",
                            StatusCodes.Status400BadRequest);
                    }
                }
                catch (BadHttpRequestException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "NVİ TC Kimlik servisi erişilemedi.");
                    throw new BadHttpRequestException(
                        "Kimlik doğrulama servisine ulaşılamadı. Lütfen daha sonra tekrar deneyin.",
                        StatusCodes.Status503ServiceUnavailable);
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

            // 5. Kart kontrolü kalktı (Checkout Form kullanılacak)

            string checkoutScript = "";
            string paymentToken = "";

            if (_iyzicoSettings.Enabled && isTrial)
            {
                var finalCallbackUrl = string.IsNullOrWhiteSpace(callbackUrl)
                    ? "https://localhost:7194/Auth/PaymentCallback"
                    : callbackUrl;

                var trialInit = await RunWithTimeoutAsync(
                    () => _iyzicoPaymentService.InitializeTrialCardValidationCheckoutAsync(
                        tenant: tenant,
                        customerEmail: dto.UserEmail,
                        customerFullName: dto.UserFullName,
                        customerPhone: dto.PhoneNumber,
                        customerIdentityNumber: dto.IdentityNumber,
                        callbackUrl: finalCallbackUrl),
                    TimeSpan.FromSeconds(90),
                    "İyzico deneme ödeme formu");

                checkoutScript = trialInit.CheckoutFormContent;
                paymentToken = trialInit.Token;
                tenant.SubscriptionReferenceCode = paymentToken;
            }
            else if (_iyzicoSettings.Enabled && !isTrial)
            {
                // Eğer dışarıdan callbackUrl gelmemişse varsayılanı kullan
                var finalCallbackUrl = string.IsNullOrWhiteSpace(callbackUrl) 
                    ? "https://localhost:7194/Auth/PaymentCallback" 
                    : callbackUrl;

                var subInit = await RunWithTimeoutAsync(
                    () => _iyzicoPaymentService.InitializeSubscriptionCheckoutFormAsync(
                        tenant: tenant,
                        customerEmail: dto.UserEmail,
                        customerFullName: dto.UserFullName,
                        customerPhone: dto.PhoneNumber,
                        customerIdentityNumber: dto.IdentityNumber,
                        planType: dto.PlanType,
                        billingCycle: dto.BillingCycle,
                        callbackUrl: finalCallbackUrl),
                    TimeSpan.FromSeconds(90),
                    "İyzico abonelik ödeme formu");

                checkoutScript = subInit.CheckoutFormContent;
                paymentToken = subInit.Token;
                
                // Token'i geçici olarak IyzicoCardToken veya SubscriptionReferenceCode alanında saklayabiliriz
                tenant.SubscriptionReferenceCode = paymentToken; 
            }

            // 6. Tenant'ı güncelle — Iyzico ödeme formu gösterilecekse kart onayı gelene kadar pasif
            var awaitingCardCheckout = _iyzicoSettings.Enabled && !string.IsNullOrWhiteSpace(paymentToken);
            if (awaitingCardCheckout)
            {
                tenant.IsSubscriptionActive = false;
                tenant.IsActive = false;
            }
            else
            {
                tenant.IsSubscriptionActive = isTrial;
                tenant.IsActive = isTrial;
            }
            tenant.IsTrial = isTrial;
            tenant.TrialUsed = isTrial;

            // ✅ DÜZELTME: PlanType Tenant entity'sinde mevcut, doğrudan set ediyoruz
            tenant.PlanType = isTrial ? "Trial" : dto.PlanType;
            tenant.BillingCycle = isTrial
                ? Appointment_SaaS.Core.Constants.BillingCycles.Monthly
                : Appointment_SaaS.Core.Constants.BillingCycles.Normalize(dto.BillingCycle);

            tenant.SubscriptionEndDate = Appointment_SaaS.Core.Utilities.SubscriptionPeriodCalculator
                .CalculateEndDateFromPayment(DateTime.Now, tenant.BillingCycle, isTrial);

            await _tenantService.UpdateAsync(tenant);

            _logger.LogInformation(
                "Kayıt tamamlandı (AppUser oluşturulmadı — ilk personel panelden eklenir). TenantId={TenantId}, Plan={Plan}",
                tenantId, dto.PlanType);

            return (tenantId, checkoutScript);
        }
        catch (BadHttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "İşletme kaydı sırasında veritabanı hatası: {Inner}", inner);
            throw new BadHttpRequestException($"İşletme kaydedilemedi: {inner}", StatusCodes.Status500InternalServerError);
        }
    }

        private static bool HasCompleteCardFields(BusinessRegistrationDto dto)
        {
            return true; // Kart alanları kalktı
        }

        public async Task<bool> VerifyPaymentAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 256)
                return false;

            var tenant = await _tenantService.GetBySubscriptionReferenceAsync(token);
            if (tenant == null)
            {
                _logger.LogWarning("Ödeme doğrulaması için Tenant bulunamadı. TokenLen={Len}", token.Length);
                return false;
            }

            var awaitingPlanChangePayment =
                !string.IsNullOrWhiteSpace(tenant.PendingPlanType)
                || (!string.IsNullOrWhiteSpace(tenant.PendingCheckoutToken)
                    && string.Equals(tenant.PendingCheckoutToken, token, StringComparison.Ordinal));

            if (tenant.IsActive && tenant.IsSubscriptionActive && !awaitingPlanChangePayment)
            {
                _logger.LogInformation("Ödeme zaten doğrulanmış (idempotent). TenantId={TenantId}", tenant.TenantID);
                return true;
            }

            try
            {
                AgentDebugLog.Write("H-E", "AuthManager.VerifyPayment", "entry", new
                {
                    tenant.TenantID,
                    tenant.PlanType,
                    tenant.IsTrial,
                    hasPending = !string.IsNullOrWhiteSpace(tenant.PendingPlanType),
                    tokenLen = token.Length
                });

                if (!string.IsNullOrWhiteSpace(tenant.PendingPlanType))
                {
                    return await _tenantPlanService.CompletePlanChangePaymentAsync(tenant, token);
                }

                if (tenant.IsTrial)
                {
                    await _iyzicoPaymentService.VerifyTrialCheckoutFormAndRefundAsync(token);
                    tenant.IsSubscriptionActive = true;
                    tenant.IsActive = true;
                    await _tenantService.UpdateSubscriptionStatusAsync(tenant, true);
                    await _tenantService.UpdateAsync(tenant);
                    return true;
                }

                var pricingRef = _iyzicoPaymentService.GetPricingPlanReferenceCode(
                    tenant.PlanType, tenant.BillingCycle);
                var verify = await _iyzicoPaymentService.VerifyCheckoutFormAsync(token, pricingRef);

                if (!string.IsNullOrWhiteSpace(verify.SubscriptionReferenceCode))
                    tenant.SubscriptionReferenceCode = verify.SubscriptionReferenceCode;
                if (!string.IsNullOrWhiteSpace(verify.CustomerReferenceCode))
                    tenant.IyzicoUserKey = verify.CustomerReferenceCode;

                tenant.IsSubscriptionActive = true;
                tenant.IsActive = true;
                tenant.IsTrial = false;
                tenant.SubscriptionEndDate = SubscriptionPeriodCalculator.CalculateEndDateFromPayment(
                    DateTime.Now, tenant.BillingCycle, isTrial: false);

                await _tenantService.UpdateSubscriptionStatusAsync(tenant, true);
                await _tenantService.UpdateAsync(tenant);

                _logger.LogInformation("Ödeme başarılı, hesap aktifleştirildi. TenantId={TenantId}", tenant.TenantID);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VerifyPaymentAsync başarısız oldu. TenantId={TenantId}", tenant.TenantID);
                return false;
            }
        }

        public static string ComputeTrialFingerprint(string phone, string businessName, string email)
        {
            var raw = $"{phone.Trim().ToLowerInvariant()}|{businessName.Trim().ToLowerInvariant()}|{email.Trim().ToLowerInvariant()}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static async Task<T> RunWithTimeoutAsync<T>(
            Func<Task<T>> action,
            TimeSpan timeout,
            string operationName)
        {
            var work = action();
            var completed = await Task.WhenAny(work, Task.Delay(timeout));
            if (completed != work)
                throw new BadHttpRequestException(
                    $"{operationName} hazırlanırken zaman aşımı oluştu. Lütfen tekrar deneyin.",
                    StatusCodes.Status504GatewayTimeout);

            return await work;
        }

        private async Task<bool> ValidateTCKimlikAsync(string tcKimlikNo, string ad, string soyad, int birthYear)
        {
            if (string.IsNullOrWhiteSpace(tcKimlikNo) || tcKimlikNo.Length != 11) return false;
            if (string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(soyad)) return false;

            ad = ad.Trim().ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
            soyad = soyad.Trim().ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));

            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <TCKimlikNoDogrula xmlns=""http://tckimlik.nvi.gov.tr/WS"">
      <TCKimlikNo>{System.Security.SecurityElement.Escape(tcKimlikNo)}</TCKimlikNo>
      <Ad>{System.Security.SecurityElement.Escape(ad)}</Ad>
      <Soyad>{System.Security.SecurityElement.Escape(soyad)}</Soyad>
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

        private Task<bool> TryReconcileSuspendedTenantAsync(Tenant tenant) =>
            _tenantPlanService.TryReconcileFromIyzicoAsync(tenant);

        private async Task EnforceTenantAccessOrThrowAsync(Tenant tenant, AppUser user)
        {
            var evaluation = _tenantAccessEvaluator.Evaluate(tenant, user);
            if (evaluation.IsAllowed)
                return;

            if (evaluation.ShouldDeactivateTenantForExpiredSubscription)
            {
                tenant.IsActive = false;
                await _tenantService.UpdateAsync(tenant);
            }

            throw new BadHttpRequestException(
                evaluation.Message ?? "Erişim reddedildi.",
                evaluation.SuggestedStatusCode);
        }

        public async Task<bool> GenerateOtpForLoginAsync(OtpLoginDto dto)
        {
            dto.PhoneNumber = OtpPhoneNormalizer.Normalize(dto.PhoneNumber);
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

            await TryReconcileSuspendedTenantAsync(tenant);
            await EnforceTenantAccessOrThrowAsync(tenant, user);

            if (user.LastOtpRequestDate.HasValue
                && (DateTime.Now - user.LastOtpRequestDate.Value).TotalSeconds < OtpLoginSettings.ResendCooldownSeconds)
                throw new BadHttpRequestException(
                    $"Lütfen yeni bir kod istemeden önce {OtpLoginSettings.ResendCooldownSeconds} saniye bekleyin.",
                    StatusCodes.Status429TooManyRequests);

            var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            // OTP'yi bağlı olduğu işletmenin Evolution instance'ından gönder.
            // Süre, mesaj gönderildikten sonra başlar (WhatsApp gecikmesi süreyi yemesin).
            bool isSent = false;

            if (!string.IsNullOrEmpty(tenant.InstanceName))
                isSent = await _evolutionApiService.SendOtpMessageAsync(
                    tenant.InstanceName, dto.PhoneNumber, otpCode);

            if (!isSent)
                isSent = await _evolutionApiService.SendOtpMessageAsync(
                    _evoSettings.DefaultInstance, dto.PhoneNumber, otpCode);

            if (!isSent)
            {
                if (_hostEnvironment.IsDevelopment())
                {
                    _logger.LogWarning(
                        "[OTP] WhatsApp gönderilemedi; Development modunda kod veritabanına yazıldı (E2E/panel: DB veya log). Phone={Phone}",
                        dto.PhoneNumber);
                }
                else
                {
                    throw new BadHttpRequestException(
                        "Doğrulama kodu gönderilemedi. Lütfen sistem yöneticisi ile iletişime geçin.",
                        StatusCodes.Status503ServiceUnavailable);
                }
            }

            user.OtpCode = otpCode;
            user.OtpExpiry = DateTime.Now.AddSeconds(OtpLoginSettings.ValiditySeconds);
            user.LastOtpRequestDate = DateTime.Now;
            await _userService.UpdateAsync(user);

            return true;
        }

        public async Task<AccessToken> VerifyOtpAndLoginAsync(OtpVerifyDto dto)
        {
            dto.PhoneNumber = OtpPhoneNormalizer.Normalize(dto.PhoneNumber);
            dto.OtpCode = NormalizeOtpCode(dto.OtpCode);
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

            await EnforceTenantAccessOrThrowAsync(tenant, user);

            // 3. OTP doğrulama + brute-force koruması
            var expectedOtp = NormalizeOtpCode(user.OtpCode);
            var submittedOtp = dto.OtpCode;
            if (string.IsNullOrEmpty(expectedOtp)
                || expectedOtp != submittedOtp
                || !user.OtpExpiry.HasValue
                || user.OtpExpiry.Value < DateTime.Now)
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