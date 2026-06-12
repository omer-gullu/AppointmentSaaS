using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Appointment_SaaS.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IAppUserService _appUserService;
        private readonly ITenantService _tenantService;
        private readonly ITenantPlanService _tenantPlanService;
        private readonly ITenantAccessEvaluator _tenantAccessEvaluator;
        private readonly ILogger<AuthController> _logger;
        private readonly IyzicoSettings _iyzicoSettings;

        public AuthController(
            IAuthService authService,
            IAppUserService appUserService,
            ITenantService tenantService,
            ITenantPlanService tenantPlanService,
            ITenantAccessEvaluator tenantAccessEvaluator,
            ILogger<AuthController> logger,
            IOptions<IyzicoSettings> iyzicoOptions)
        {
            _authService = authService;
            _appUserService = appUserService;
            _tenantService = tenantService;
            _tenantPlanService = tenantPlanService;
            _tenantAccessEvaluator = tenantAccessEvaluator;
            _logger = logger;
            _iyzicoSettings = iyzicoOptions.Value;
        }

        /// <summary>
        /// Yeni işletme kaydı oluşturur. İyzico açıksa <c>checkoutFormContent</c> dolu döner; kapalıysa <c>paymentCheckoutSkipped</c> true olur.
        /// WebUI, ödeme sonrası dönüş için <see cref="BusinessRegistrationDto.PaymentCallbackUrl"/> göndermelidir (örn. https://host/Auth/PaymentCallback).
        /// </summary>
        [HttpPost("register-business")]
        public async Task<IActionResult> RegisterBusinessOwner(BusinessRegistrationDto dto)
        {
            try
            {
                var callback = dto.PaymentCallbackUrl?.Trim() ?? string.Empty;
                var result = await _authService.RegisterBusinessOwnerAsync(dto, callback);
                var script = result.CheckoutFormScript ?? string.Empty;
                var skippedBecauseIyzicoDisabled = !_iyzicoSettings.Enabled;
                return Ok(new
                {
                    Status = "Başarılı",
                    TenantId = result.TenantId,
                    CheckoutFormContent = script,
                    PaymentCheckoutSkipped = skippedBecauseIyzicoDisabled,
                    PaymentCheckoutSkipReason = skippedBecauseIyzicoDisabled
                        ? "İyzico devre dışı (IyzicoSettings.Enabled=false). Sandbox kart / 3DS için API appsettings.Development.json içinde Enabled=true ve sandbox ApiKey/SecretKey girin."
                        : null
                });
            }
            catch (BadHttpRequestException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İşletme kaydı sırasında hata oluştu.");
                if (ex.Message.Contains("daha önce kayıt olunmuş") || ex.Message.Contains("İşletme kaydedilemedi"))
                {
                    return BadRequest(new { Message = ex.Message });
                }
                return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
            }
        }

        /// <summary>
        /// İyzico'dan dönen Checkout Form sonucunu doğrular.
        /// </summary>
        [HttpPost("payment-callback")]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentCallback([FromBody] PaymentCallbackDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Token))
                return BadRequest(new { Message = "Geçersiz ödeme bildirimi." });

            if (dto.Token.Length > 256)
                return BadRequest(new { Message = "Geçersiz ödeme bildirimi." });

            var isSuccess = await _authService.VerifyPaymentAsync(dto.Token.Trim());
            if (isSuccess)
                return Ok(new { Status = "Başarılı" });
            
            return BadRequest(new { Message = "Ödeme onaylanamadı." });
        }

        /// <summary>
        /// Oturum açıkken tenant aboneliği / trial durumunu doğrular (WebUI middleware için).
        /// </summary>
        [HttpGet("session-access")]
        [Authorize]
        public async Task<IActionResult> SessionAccess()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tenantIdStr = User.FindFirstValue("TenantId");
            if (!int.TryParse(userIdStr, out var userId) || !int.TryParse(tenantIdStr, out var tenantId))
                return Unauthorized();

            var user = await _appUserService.GetByIdAsync(userId);
            if (user == null || user.TenantID != tenantId)
                return Unauthorized();

            var tenant = await _tenantService.GetByIdAsync(tenantId);
            if (tenant == null)
                return StatusCode(StatusCodes.Status403Forbidden, new { Message = "İşletme bulunamadı." });

            await _tenantPlanService.TryReconcileFromIyzicoAsync(tenant);

            var evaluation = _tenantAccessEvaluator.Evaluate(tenant, user);
            if (evaluation.IsAllowed)
                return NoContent();

            if (evaluation.ShouldDeactivateTenantForExpiredSubscription && tenant.IsActive)
            {
                tenant.IsActive = false;
                await _tenantService.UpdateAsync(tenant);
            }

            return StatusCode(evaluation.SuggestedStatusCode, new { Message = evaluation.Message });
        }

        /// <summary>
        /// Telefon numarasına WhatsApp/SMS üzerinden OTP gönderir.
        /// </summary>
        [HttpPost("generate-otp")]
        public async Task<IActionResult> GenerateOtp(OtpLoginDto dto)
        {
            try
            {
                var result = await _authService.GenerateOtpForLoginAsync(dto);
                if (result)
                    return Ok(new { Status = "Başarılı", Message = "Doğrulama kodu WhatsApp/SMS olarak gönderildi." });
                else
                    return BadRequest(new { Message = "Mesaj gönderilemedi, servis kapalı olabilir." });
            }
            catch (BadHttpRequestException ex)
            {
                return StatusCode(ex.StatusCode, new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP oluşturulurken hata oluştu.");
                return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
            }
        }

        /// <summary>
        /// OTP kodunu doğrular ve JWT token döner.
        /// 3 yanlış denemede hesap 15 dakika kilitlenir → 429 döner.
        /// </summary>
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp(OtpVerifyDto dto)
        {
            try
            {
                var result = await _authService.VerifyOtpAndLoginAsync(dto);
                return Ok(result); // AccessToken döner
            }
            catch (InvalidOperationException ex)
            {
                // OtpManager brute-force kilidi devreye girdi
                // 429 Too Many Requests: istemciye "bekle" sinyali verir
                _logger.LogWarning("OTP brute-force kilidi tetiklendi. Phone={Phone}", dto.PhoneNumber);
                return StatusCode(429, new { Message = ex.Message });
            }
            catch (BadHttpRequestException ex)
            {
                return StatusCode(ex.StatusCode, new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP doğrulanırken beklenmedik hata oluştu.");
                return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
            }
        }
    }
}