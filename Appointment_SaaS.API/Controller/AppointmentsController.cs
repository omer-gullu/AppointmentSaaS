using Appointment_SaaS.API.Authorization;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Utilities;
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

            var access = await _tenantService.EvaluateOperationalAccessAsync(tenant.TenantID);
            if (!access.IsAllowed)
                return StatusCode(access.SuggestedStatusCode, new { Message = access.Message });

            var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenant.TenantID);
            if (scopeDenied != null)
                return scopeDenied;

            dto.TenantID = tenant.TenantID;

            if (!dto.AppUserID.HasValue || dto.AppUserID.Value <= 0)
            {
                var smartStaffId = await _appointmentService.GetStaffWithFewestAppointmentsAsync(tenant.TenantID, dto.StartDate);
                if (!smartStaffId.HasValue)
                    return BadRequest(new { Message = "Randevu oluşturmak için önce işletmeye bağlı en az bir personel tanımlanmalı." });
                dto.AppUserID = smartStaffId.Value;
            }

            var orderedServiceIds = new List<int>();
            if (dto.ServiceIds != null && dto.ServiceIds.Count > 0)
            {
                foreach (var id in dto.ServiceIds)
                {
                    if (id > 0 && !orderedServiceIds.Contains(id))
                        orderedServiceIds.Add(id);
                }
            }
            if (orderedServiceIds.Count == 0 && dto.ServiceID > 0)
                orderedServiceIds.Add(dto.ServiceID);

            if (orderedServiceIds.Count == 0)
                return BadRequest(new { Message = "En az bir hizmet seçilmelidir (serviceID veya serviceIds)." });

            var totalMinutes = 0;
            var serviceNames = new List<string>();
            foreach (var sid in orderedServiceIds)
            {
                var svc = await _serviceService.GetByIdAsync(sid);
                if (svc == null || svc.TenantID != tenant.TenantID)
                    return BadRequest(new { Message = $"Hizmet bulunamadı veya bu işletmeye ait değil (ServiceID={sid})." });
                totalMinutes += svc.DurationInMinutes;
                serviceNames.Add(svc.Name);
            }

            dto.ServiceID = orderedServiceIds[0];
            dto.ServiceIds = orderedServiceIds;
            dto.EndDate = dto.StartDate.AddMinutes(totalMinutes);
            var combinedServiceName = string.Join(", ", serviceNames);

            var appointmentId = await _appointmentService.AddAppointmentAsync(dto);
            var created = await _appointmentService.GetByIdAsync(appointmentId);

            return Ok(new
            {
                Message = "Randevu başarıyla onaylandı.",
                ID = appointmentId,
                GoogleEventId = created?.GoogleEventID,
                CustomerName = dto.CustomerName,
                CustomerPhone = dto.CustomerPhone,
                ServiceName = combinedServiceName,
                ServiceIds = orderedServiceIds,
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

            var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, dto.TenantId);
            if (scopeDenied != null)
                return scopeDenied;

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

            int? scopeTenant = ControllerTenantAccess.GetWebhookScopedTenantId(HttpContext);
            if (!scopeTenant.HasValue && ControllerTenantAccess.TryGetClaimTenantId(User, out var claimTenant))
                scopeTenant = claimTenant;

            if (scopeTenant.HasValue)
                _appointmentService.ReleaseSlotLock(dto.LockKey, scopeTenant);
            else if (SlotLockKeyParser.TryGetTenantId(dto.LockKey, out var lockTenant))
            {
                var denied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, lockTenant);
                if (denied != null)
                    return denied;
                _appointmentService.ReleaseSlotLock(dto.LockKey, lockTenant);
            }
            else
                return BadRequest(new { Message = "Geçersiz kilit anahtarı." });

            return Ok(new { Message = "Kilit bırakıldı." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { Message = ex.Message });
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
            // JWT ile giriş yapan kullanıcı sadece kendi tenant'ını görebilir
            var denied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenantId);
            if (denied != null)
                return denied;

            var appointments = await _appointmentService.GetListItemsByTenantIdAsync(tenantId);
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
    [Authorize(AuthenticationSchemes = "Bearer,WebhookScheme")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] AppointmentCreateDto dto)
    {
        try
        {
            var appointment = await _appointmentService.GetByIdAsync(id);
            if (appointment == null)
                return NotFound(new { Message = "Randevu bulunamadı." });

            var denied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, appointment.TenantID);
            if (denied != null)
                return denied;

            var tenant = await _tenantService.GetByIdAsync(appointment.TenantID);
            if (tenant == null)
                return BadRequest(new { Message = "İşletme bulunamadı." });

            var orderedServiceIds = new List<int>();
            if (dto.ServiceIds != null && dto.ServiceIds.Count > 0)
            {
                foreach (var sid in dto.ServiceIds)
                {
                    if (sid > 0 && !orderedServiceIds.Contains(sid))
                        orderedServiceIds.Add(sid);
                }
            }
            if (orderedServiceIds.Count == 0 && dto.ServiceID > 0)
                orderedServiceIds.Add(dto.ServiceID);

            if (orderedServiceIds.Count == 0)
                return BadRequest(new { Message = "En az bir hizmet seçilmelidir (serviceID veya serviceIds)." });

            var totalMinutes = 0;
            foreach (var sid in orderedServiceIds)
            {
                var svc = await _serviceService.GetByIdAsync(sid);
                if (svc == null || svc.TenantID != tenant.TenantID)
                    return BadRequest(new { Message = $"Hizmet bulunamadı veya bu işletmeye ait değil (ServiceID={sid})." });
                totalMinutes += svc.DurationInMinutes;
            }

            var previousAppUserID = appointment.AppUserID;

            appointment.CustomerName = dto.CustomerName;
            appointment.CustomerPhone = dto.CustomerPhone;
            appointment.ServiceID = orderedServiceIds[0];
            appointment.StartDate = dto.StartDate;
            appointment.EndDate = dto.StartDate.AddMinutes(totalMinutes);
            appointment.Note = dto.Note ?? appointment.Note;

            if (dto.AppUserID.HasValue && dto.AppUserID.Value > 0)
                appointment.AppUserID = dto.AppUserID.Value;

            await _appointmentService.UpdateAsync(appointment, previousAppUserID, orderedServiceIds);
            return Ok(new
            {
                Message = "Randevu başarıyla güncellendi.",
                AppointmentID = appointment.AppointmentID,
                StartDate = appointment.StartDate,
                EndDate = appointment.EndDate,
                AppUserID = appointment.AppUserID
            });
        }
        catch (BadHttpRequestException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Randevu güncellenirken hata oluştu. ID: {id}", id);
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }

    // ─── Randevu sil ─────────────────────────────────────────────────────────
    [Authorize(AuthenticationSchemes = "Bearer,WebhookScheme")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var appointment = await _appointmentService.GetByIdAsync(id);
            if (appointment == null)
                return NotFound(new { Message = "Randevu bulunamadı." });

            var denied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, appointment.TenantID);
            if (denied != null)
                return denied;

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

            var appointment = await _appointmentService.GetByIdAsync(id);
            if (appointment == null)
                return NotFound(new { Message = "Randevu bulunamadı." });

            var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, appointment.TenantID);
            if (scopeDenied != null)
                return scopeDenied;

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

            var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenantId);
            if (scopeDenied != null)
                return scopeDenied;

            var history = await _appointmentService.GetCustomerHistoryAsync(phone, tenantId);

            // Aktif randevuları da ekle
            var activeAppointments = await _appointmentService.GetActiveAppointmentsByPhoneAsync(phone, tenantId);

            return Ok(new
            {
                history.IsReturningCustomer,
                history.TotalVisits,
                history.CustomerName,
                history.LastVisitDate,
                history.LastServiceName,
                history.LastVisitStatus,
                history.SummaryForAI,
                ActiveAppointments = activeAppointments
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Müşteri geçmişi alınırken hata oluştu. Phone={Phone}", phone);
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }

    // ─── Yarın Hatırlatma (Pro / Business) ───────────────────────────────────
    /// <summary>n8n cron: gönderilecek hatırlatmaları listeler (Pro/Business, yarın, henüz gönderilmemiş).</summary>
    [AllowAnonymous]
    [HttpGet("reminders/pending")]
    public async Task<IActionResult> GetPendingReminders()
    {
        try
        {
            var pending = await _appointmentService.GetPendingRemindersAsync();
            return Ok(pending);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bekleyen hatırlatmalar alınırken hata.");
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }

    /// <summary>n8n cron: tüm bekleyen yarın hatırlatmalarını WhatsApp ile gönderir.</summary>
    [AllowAnonymous]
    [HttpPost("reminders/run")]
    public async Task<IActionResult> RunReminders()
    {
        try
        {
            var result = await _appointmentService.SendPendingRemindersAsync();
            return Ok(new
            {
                result.Total,
                result.Sent,
                result.Failed,
                result.Errors,
                Message = $"{result.Sent}/{result.Total} hatırlatma gönderildi."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hatırlatma gönderimi sırasında hata.");
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

            var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenantId);
            if (scopeDenied != null)
                return scopeDenied;

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
    /// <summary>n8n: müsait slotlar. X-Auth-Token veya JWT (WebhookAuthMiddleware).</summary>
    [AllowAnonymous]
    [HttpGet("available-slots")]
    public async Task<IActionResult> GetAvailableSlots(
      [FromQuery] string instanceName,
      [FromQuery] int staffId,
      [FromQuery] string date,
      [FromQuery] int durationMinutes = 30,
      [FromQuery] string? requestedTime = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(instanceName))
                return BadRequest(new { Message = "instanceName gereklidir." });

            var tenant = await _tenantService.GetContextByInstanceAsync(instanceName);
            if (tenant == null)
                return NotFound(new { Message = "İşletme bulunamadı." });

            var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenant.TenantID);
            if (scopeDenied != null)
                return scopeDenied;

            if (!DateTime.TryParse(date, out var targetDate))
                return BadRequest(new { Message = "Geçersiz tarih formatı. YYYY-MM-DD kullanın." });

            // 🔴 TATİL KONTROLÜ
            var dateOnly = DateOnly.FromDateTime(targetDate);
            var holiday = tenant.Holidays.FirstOrDefault(h => h.Date == dateOnly);
            if (holiday != null)
                return Ok(new
                {
                    Date = targetDate.ToString("dd.MM.yyyy"),
                    IsHoliday = true,
                    Message = $"Bu gün {holiday.Name} nedeniyle kapalıdır.",
                    AvailableSlots = Array.Empty<object>()
                });

            // geri kalan kod aynı kalıyor
            if (staffId > 0)
            {
                var slots = await _appointmentService.GetAvailableSlotsByStaffAsync(
                    tenant.TenantID, staffId, targetDate, durationMinutes, count: 100, requestedTime: requestedTime);
                if (!string.IsNullOrEmpty(requestedTime))
                {
                    bool isAvailable = slots.Any();
                    return Ok(new
                    {
                        Date = targetDate.ToString("dd.MM.yyyy"),
                        StaffId = staffId,
                        RequestedTime = requestedTime,
                        IsAvailable = isAvailable,
                        Message = isAvailable ? $"{requestedTime} saati müsait." : $"{requestedTime} saati müsait değil.",
                        NearestSlots = isAvailable ? slots : await _appointmentService.GetAvailableSlotsByStaffAsync(
                            tenant.TenantID, staffId, targetDate, durationMinutes, count: 3)
                    });
                }
                return Ok(new { Date = targetDate.ToString("dd.MM.yyyy"), StaffId = staffId, AvailableSlots = slots, TotalSlots = slots.Count });
            }
            else
            {
                var allSlots = await _appointmentService.GetAvailableSlotsForAllStaffAsync(
                    tenant.TenantID, targetDate, durationMinutes, count: 100);
                return Ok(new { Date = targetDate.ToString("dd.MM.yyyy"), Staff = allSlots });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Müsait slotlar alınırken hata. Instance={Instance}", instanceName);
            return StatusCode(500, new { Message = "Sistemsel bir hata oluştu." });
        }
    }

    // ─── Müşterinin Aktif Randevuları (WhatsApp / n8n) ───────────────────────
    /// <summary>
    /// n8n: WhatsApp'tan mesaj atan müşterinin tarihi geçmemiş ve iptal edilmemiş
    /// randevularını döndürür. Arama remoteJid veya telefon üzerinden, varyasyon
    /// duyarsız (+90 / 90 / 0 / ham çekirdek / @s.whatsapp.net) yapılır.
    /// GET /api/Appointments/my-active-appointments?instanceName=...&amp;remoteJid=...|&amp;phone=...
    /// </summary>
    [AllowAnonymous]
    [HttpGet("my-active-appointments")]
    public async Task<IActionResult> GetMyActiveAppointments(
        [FromQuery] string instanceName,
        [FromQuery] string? remoteJid = null,
        [FromQuery] string? phone = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(instanceName))
                return BadRequest(new { Message = "instanceName gereklidir." });

            if (string.IsNullOrWhiteSpace(remoteJid) && string.IsNullOrWhiteSpace(phone))
                return BadRequest(new { Message = "remoteJid veya phone değerlerinden en az biri gereklidir." });

            var tenant = await _tenantService.GetContextByInstanceAsync(instanceName);
            if (tenant == null)
                return NotFound(new { Message = "İşletme bulunamadı." });

            var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenant.TenantID);
            if (scopeDenied != null)
                return scopeDenied;

            var lookup = !string.IsNullOrWhiteSpace(remoteJid) ? remoteJid! : phone!;

            var items = await _appointmentService.GetActiveAppointmentsForCustomerAsync(tenant.TenantID, lookup);

            return Ok(new
            {
                hasActiveAppointment = items.Count > 0,
                totalCount = items.Count,
                message = items.Count > 0 ? "Aktif randevu bulundu." : "Aktif randevu bulunamadı.",
                appointments = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "my-active-appointments hatası. Instance={Instance}", instanceName);
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