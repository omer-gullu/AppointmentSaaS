using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;
        private readonly AppDbContext _db;
        private readonly ILogger<AuditLogsController> _logger;

        public AuditLogsController(
            IAuditLogService auditLogService,
            AppDbContext db,
            ILogger<AuditLogsController> logger)
        {
            _auditLogService = auditLogService;
            _db = db;
            _logger = logger;
        }

        // ─── Tüm logları getir (Sadece Admin) ────────────────────────────────
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll()
        {
            var logs = await _auditLogService.GetAllLogsAsync();
            return Ok(logs);
        }

        // ─── Belirli tenant'ın logları ────────────────────────────────────────
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetByTenant(int tenantId)
        {
            var logs = await _auditLogService.GetLogsByTenantAsync(tenantId);
            return Ok(logs);
        }

        // ─── Kaynak bazlı filtreleme (Admin paneli için) ──────────────────────
        /// <summary>
        /// GET /api/AuditLogs/source/n8n → Sadece n8n kaynaklı loglar
        /// GET /api/AuditLogs/source/API → Sadece API kaynaklı loglar
        /// </summary>
        [HttpGet("source/{source}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetBySource(string source)
        {
            var logs = await _auditLogService.GetLogsBySourceAsync(source);
            return Ok(logs);
        }

        // ─── Seviye bazlı filtreleme (Sadece hatalar) ─────────────────────────
        /// <summary>
        /// GET /api/AuditLogs/errors → Sadece Error seviyesindeki loglar
        /// Admin panelinde kırmızı bildirim rozeti için kullanılır.
        /// </summary>
        [HttpGet("errors")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetErrors()
        {
            var logs = await _auditLogService.GetLogsByLevelAsync("Error");
            return Ok(logs);
        }

        // ─── n8n Workflow Hata Bildirimi ──────────────────────────────────────
        /// <summary>
        /// n8n Error Workflow tarafından çağrılır.
        /// Workflow tıkandığında hangi node'da, hangi işletmede ve neden hata aldığını kaydeder.
        /// X-Auth-Token ile korunur (WebhookAuthMiddleware).
        /// POST /api/AuditLogs/workflow-error
        /// </summary>
        [HttpPost("workflow-error")]
        [AllowAnonymous] // WebhookAuthMiddleware X-Auth-Token ile korur
        public async Task<IActionResult> ReportWorkflowError([FromBody] WorkflowErrorDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.ErrorMessage))
                    return BadRequest(new { message = "ErrorMessage boş olamaz." });

                var log = new AuditLog
                {
                    Action = "WorkflowError",
                    EntityName = dto.NodeName ?? "Unknown Node",
                    EntityId = dto.WorkflowId ?? "Unknown Workflow",
                    NewValues = dto.ErrorMessage,
                    OldValues = dto.InputData,
                    TenantId = dto.TenantId,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Timestamp = DateTime.UtcNow,
                    Source = "n8n",
                    LogLevel = "Error"
                };

                _db.AuditLogs.Add(log);
                await _db.SaveChangesAsync();

                _logger.LogError(
                    "[n8n WorkflowError] Node={Node} WorkflowId={WorkflowId} TenantId={TenantId} Error={Error}",
                    dto.NodeName, dto.WorkflowId, dto.TenantId, dto.ErrorMessage);

                return Ok(new { status = "logged" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WorkflowError] Log kaydedilemedi.");
                return StatusCode(500, new { message = "Log kaydedilemedi." });
            }
        }
    }

    // ─── n8n'den gelen hata payload'ı ────────────────────────────────────────
    public class WorkflowErrorDto
    {
        /// <summary>Hatanın oluştuğu n8n node adı</summary>
        public string? NodeName { get; set; }

        /// <summary>n8n Workflow ID</summary>
        public string? WorkflowId { get; set; }

        /// <summary>Hata mesajı</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>Hatayı tetikleyen input verisi (isteğe bağlı)</summary>
        public string? InputData { get; set; }

        /// <summary>Hangi işletme için hata oluştu</summary>
        public int? TenantId { get; set; }
    }
}
