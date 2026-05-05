using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.API.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;
    private readonly ITenantService _tenantService;
    private readonly IServiceService _serviceService;
    private readonly IAppUserService _appUserService;
    private readonly ILogger<AppointmentsController> _logger;

    public AppointmentsController(
        IAppointmentService appointmentService,
        ITenantService tenantService,
        IServiceService serviceService,
        IAppUserService appUserService,
        ILogger<AppointmentsController> logger)
    {
        _appointmentService = appointmentService;
        _tenantService = tenantService;
        _serviceService = serviceService;
        _appUserService = appUserService;
        _logger = logger;
    }

    // ─── Randevu Oluşturma ────────────────────────────────────────────────────
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AppointmentCreateDto dto)
    {
        try
        {
            var tenant = await _tenantService.GetContextByInstanceAsync(dto.BusinessPhone)
                      ?? await _tenantService.GetByPhoneNumberAsync(dto.BusinessPhone);

            if (tenant == null)
                return BadRequest(new { Message = "Geçersiz işletme. Bu numaraya veya instance adına kayıtlı bir işletme bulunamadı." });

            dto.TenantID = tenant.TenantID;

            if (!dto.AppUserID.HasValue || dto.AppUserID.Value <= 0)
            {
                var smartStaffId = await _appointmentService.GetStaffWithFewestAppointmentsAsync(tenant.TenantID, dto.StartDate);
                if (!smartStaffId.HasValue)
                    return BadRequest(new { Message = "Randevu oluşturmak için önce işletmeye bağlı en az bir personel tanımlanmalı." });
                dto.AppUserID = smartStaffId.Value;
            }

            var service = await _serviceService.GetByIdAsync(dto.ServiceID);
            if (service == null)
                return BadRequest(new { Message = "Seçilen hizmet bulunamadı." });

            dto.EndDate = dto.StartDate.AddMinutes(service.DurationInMinutes);

            var appointmentId = await _appointmentService.AddAppointmentAsync(dto);

            return Ok(new
            {
                Message = "Randevu başarıyla onaylandı.",
                ID = appointmentId,
                CustomerName = dto.CustomerName,
                CustomerPhone = dto.CustomerPhone,
                ServiceName = service.Name,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                AppUserID = dto.AppUserID
            });
        }
        catch (BadHttpRequestException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Randevu oluşturulurken beklenmedik hata oluştu.");
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }

    // ─── Slot Kilitle (n8n kendi_sistemine_kaydet'ten ÖNCE çağırır) ──────────
    /// <summary>
    /// n8n'in randevu kaydetmeden önce slotu reserve etmesi için.
    /// Aynı slota ikinci bir istek gelirse 409 Conflict döner.
    /// POST /api/Appointments/lock
    /// Body: { "tenantId": 1, "startDate": "2026-05-01T14:00:00" }
    /// </summary>
    [AllowAnonymous]
    [HttpPost("lock")]
    public IActionResult LockSlot([FromBody] SlotLockDto dto)
    {
        try
        {
            if (dto.TenantId <= 0 || dto.StartDate == default)
                return BadRequest(new { Message = "TenantId ve StartDate gereklidir." });

            var acquired = _appointmentService.TryAcquireSlotLock(dto.TenantId, dto.StartDate, out string lockKey);

            if (!acquired)
            {
                return Conflict(new
                {
                    Message = "Bu saat dilimi şu an başka biri tarafından işleniyor. Lütfen birkaç saniye sonra tekrar deneyin.",
                    Locked = true
                });
            }

            return Ok(new { Locked = false, LockKey = lockKey, Message = "Slot rezerve edildi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Slot kilitlenirken hata oluştu.");
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }

    // ─── Slot Kilidi Bırak (randevu kaydedildikten veya hata olursa) ─────────
    /// <summary>
    /// Randevu başarıyla kaydedildikten veya hata oluşunca kilidi bırakır.
    /// POST /api/Appointments/unlock
    /// Body: { "lockKey": "slot_lock:1:202605011400" }
    /// </summary>
    [AllowAnonymous]
    [HttpPost("unlock")]
    public IActionResult UnlockSlot([FromBody] SlotUnlockDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.LockKey))
                return BadRequest(new { Message = "LockKey gereklidir." });

            _appointmentService.ReleaseSlotLock(dto.LockKey);
            return Ok(new { Message = "Kilit bırakıldı." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Slot kilidi bırakılırken hata oluştu.");
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }

    // ─── Tenant'a göre randevuları listele ────────────────────────────────────
    [HttpGet("tenant/{tenantId}")]
    public async Task<IActionResult> GetByTenant(int tenantId)
    {
        try
        {
            var appointments = await _appointmentService.GetAllByTenantIdAsync(tenantId);
            if (appointments == null || !appointments.Any())
                return NotFound(new { Message = "Bu dükkan için henüz bir randevu kaydı bulunamadı." });
            return Ok(appointments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Randevular listelenirken hata oluştu. TenantId: {tenantId}", tenantId);
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }

    // ─── Randevu güncelle ─────────────────────────────────────────────────────
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] AppointmentCreateDto dto)
    {
        try
        {
            var appointment = await _appointmentService.GetByIdAsync(id);
            if (appointment == null)
                return NotFound(new { Message = "Randevu bulunamadı." });

            var service = await _serviceService.GetByIdAsync(dto.ServiceID);
            if (service == null)
                return BadRequest(new { Message = "Seçilen hizmet bulunamadı." });

            appointment.CustomerName = dto.CustomerName;
            appointment.CustomerPhone = dto.CustomerPhone;
            appointment.ServiceID = dto.ServiceID;
            appointment.StartDate = dto.StartDate;
            appointment.EndDate = dto.StartDate.AddMinutes(service.DurationInMinutes);
            appointment.Note = dto.Note ?? appointment.Note;
            if (dto.GoogleEventID != null) appointment.GoogleEventID = dto.GoogleEventID;

            await _appointmentService.UpdateAsync(appointment);
            return Ok(new { Message = "Randevu başarıyla güncellendi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Randevu güncellenirken hata oluştu. ID: {id}", id);
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }

    // ─── Randevu sil ─────────────────────────────────────────────────────────
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var appointment = await _appointmentService.GetByIdAsync(id);
            if (appointment == null)
                return NotFound(new { Message = "Randevu bulunamadı." });

            await _appointmentService.DeleteAsync(appointment);
            return Ok(new { Message = "Randevu başarıyla silindi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Randevu silinirken hata oluştu. ID: {id}", id);
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }

    // ─── Google Event ID güncelle ─────────────────────────────────────────────
    [AllowAnonymous]
    [HttpPatch("{id}/google-event")]
    public async Task<IActionResult> UpdateGoogleEvent(int id, [FromBody] GoogleEventUpdateDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.GoogleEventId))
                return BadRequest(new { Message = "GoogleEventId boş olamaz." });

            var updated = await _appointmentService.UpdateGoogleEventIdAsync(id, dto.GoogleEventId);
            if (!updated)
                return NotFound(new { Message = "Randevu bulunamadı." });

            return Ok(new { Message = "Google Takvim ID başarıyla kaydedildi.", AppointmentId = id, GoogleEventId = dto.GoogleEventId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Event ID güncellenirken hata oluştu. ID: {id}", id);
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }

    // ─── Müşteri Geçmişi ──────────────────────────────────────────────────────
    [AllowAnonymous]
    [HttpGet("customer/{phone}")]
    public async Task<IActionResult> GetCustomerHistory(string phone, [FromQuery] int tenantId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(phone) || tenantId <= 0)
                return BadRequest(new { Message = "Telefon ve tenantId gereklidir." });

            var history = await _appointmentService.GetCustomerHistoryAsync(phone, tenantId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Müşteri geçmişi alınırken hata oluştu. Phone={Phone}", phone);
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }

    // ─── Yarınki Randevular (Hatırlatma) ─────────────────────────────────────
    [AllowAnonymous]
    [HttpGet("tomorrow")]
    public async Task<IActionResult> GetTomorrowAppointments([FromQuery] int tenantId)
    {
        try
        {
            if (tenantId <= 0)
                return BadRequest(new { Message = "Geçerli bir tenantId gereklidir." });

            var appointments = await _appointmentService.GetTomorrowAppointmentsAsync(tenantId);

            var result = appointments.Select(a => new
            {
                AppointmentID = a.AppointmentID,
                CustomerName = a.CustomerName,
                CustomerPhone = a.CustomerPhone,
                StartDate = a.StartDate.ToString("dd.MM.yyyy HH:mm"),
                ServiceName = a.Service?.Name ?? "Bilinmiyor"
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yarınki randevular alınırken hata oluştu. TenantId={TenantId}", tenantId);
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }
}

// ─── DTO'lar ──────────────────────────────────────────────────────────────────
public class SlotLockDto
{
    public int TenantId { get; set; }
    public DateTime StartDate { get; set; }
}

public class SlotUnlockDto
{
    public string LockKey { get; set; } = string.Empty;
}