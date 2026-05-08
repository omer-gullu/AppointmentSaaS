using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;
        private readonly ILogger<FeedbacksController> _logger;

        public FeedbacksController(IFeedbackService feedbackService, ILogger<FeedbacksController> logger)
        {
            _feedbackService = feedbackService;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FeedbackCreateDto dto)
        {
            try
            {
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