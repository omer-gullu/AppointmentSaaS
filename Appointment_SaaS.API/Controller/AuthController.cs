using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Appointment_SaaS.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Yeni işletme kaydı oluşturur.
        /// </summary>
        [HttpPost("register-business")]
        public async Task<IActionResult> RegisterBusinessOwner(BusinessRegistrationDto dto)
        {
            try
            {
                var tenantId = await _authService.RegisterBusinessOwnerAsync(dto);
                return Ok(new { Status = "Başarılı", TenantId = tenantId });
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
                return BadRequest(new { Message = ex.Message });
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
                // Geçersiz/süresi dolmuş OTP gibi iş mantığı hataları
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP doğrulanırken beklenmedik hata oluştu.");
                return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
            }
        }
    }
}