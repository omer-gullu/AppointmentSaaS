using Appointment_SaaS.API.Authorization;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Validation;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;
        private readonly ITenantService _tenantService;
        private readonly IValidator<FeedbackCreateRequest> _feedbackValidator;
        private readonly ILogger<FeedbacksController> _logger;

        public FeedbacksController(
            IFeedbackService feedbackService,
            ITenantService tenantService,
            IValidator<FeedbackCreateRequest> feedbackValidator,
            ILogger<FeedbacksController> logger)
        {
            _feedbackService = feedbackService;
            _tenantService = tenantService;
            _feedbackValidator = feedbackValidator;
            _logger = logger;
        }

        /// <summary>WebhookAuthMiddleware: X-Auth-Token veya JWT. TenantId doğrulanır.</summary>
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FeedbackCreateDto dto)
        {
            try
            {
                var validation = await _feedbackValidator.ValidateAsync(new FeedbackCreateRequest
                {
                    TenantId = dto.TenantId,
                    FeedbackType = dto.FeedbackType,
                    Message = dto.Message
                });
                if (!validation.IsValid)
                    return BadRequest(new { message = validation.Errors.First().ErrorMessage });

                dto.FeedbackType = HtmlInputSanitizer.SanitizeText(dto.FeedbackType);
                dto.Message = HtmlInputSanitizer.SanitizeText(dto.Message);

                var tenant = await _tenantService.GetByIdAsync(dto.TenantId);
                if (tenant == null)
                    return NotFound(new { message = "İşletme bulunamadı." });

                var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, dto.TenantId);
                if (scopeDenied != null)
                    return scopeDenied;

                var feedback = new Feedback
                {
                    TenantID = dto.TenantId,
                    FeedbackType = dto.FeedbackType,
                    Message = dto.Message,
                    SentAt = DateTime.Now
                };
                var id = await _feedbackService.AddAsync(feedback);
                return Ok(new { id, message = "Geri bildiriminiz alındı." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feedback kaydedilemedi.");
                return StatusCode(500, new { message = "Sistemsel bir hata oluştu." });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var feedbacks = await _feedbackService.GetAllAsync();
            return Ok(feedbacks);
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            await _feedbackService.MarkAsReadAsync(id);
            return Ok(new { message = "Okundu olarak işaretlendi." });
        }
    }

    public class FeedbackCreateDto
    {
        public int TenantId { get; set; }
        public string FeedbackType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}