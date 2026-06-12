using System.Collections.Generic;
using System.Globalization;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Constants;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Context;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Services;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Data;

namespace Appointment_SaaS.Business.Concrete;

public class AppointmentManager : IAppointmentService
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IMapper _mapper;
    private readonly ITenantRepository _tenantRepository;
    private readonly IEvolutionApiService _evolutionApiService;
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<AppointmentManager> _logger;
    private readonly IMemoryCache _cache;
    private readonly IGoogleCalendarService _googleCalendarService;

    public AppointmentManager(
        IAppointmentRepository appointmentRepository,
        IMapper mapper,
        ITenantRepository tenantRepository,
        IEvolutionApiService evolutionApiService,
        AppDbContext db,
        ITenantProvider tenantProvider,
        ILogger<AppointmentManager> logger,
        IMemoryCache cache,
        IGoogleCalendarService googleCalendarService)
    {
        _appointmentRepository = appointmentRepository;
        _mapper = mapper;
        _tenantRepository = tenantRepository;
        _evolutionApiService = evolutionApiService;
        _db = db;
        _tenantProvider = tenantProvider;
        _logger = logger;
        _cache = cache;
        _googleCalendarService = googleCalendarService;
    }

    private static List<int> ResolveOrderedServiceIds(AppointmentCreateDto dto)
    {
        var ids = new List<int>();
        if (dto.ServiceIds != null)
        {
            foreach (var id in dto.ServiceIds)
            {
                if (id > 0 && !ids.Contains(id))
                    ids.Add(id);
            }
        }
        if (ids.Count == 0 && dto.ServiceID > 0)
            ids.Add(dto.ServiceID);
        return ids;
    }

    private async Task<string> FormatServiceNamesByIdsAsync(IReadOnlyList<int> orderedIds)
    {
        if (orderedIds.Count == 0)
            return "Randevu";
        var names = new List<string>();
        foreach (var id in orderedIds)
        {
            var name = await _db.Services.AsNoTracking()
                .Where(s => s.ServiceID == id)
                .Select(s => s.Name)
                .FirstOrDefaultAsync();
            names.Add(name ?? "?");
        }
        return string.Join(", ", names);
    }

    private async Task<string> GetDisplayedServiceNamesAsync(int appointmentId, int fallbackServiceId)
    {
        var linkNames = await _db.AppointmentServiceLinks
            .AsNoTracking()
            .Where(l => l.AppointmentID == appointmentId)
            .OrderBy(l => l.SortOrder)
            .Join(_db.Services.AsNoTracking(),
                l => l.ServiceID,
                s => s.ServiceID,
                (_, s) => s.Name)
            .ToListAsync();
        if (linkNames.Count > 0)
            return string.Join(", ", linkNames);
        var single = await _db.Services.AsNoTracking()
            .Where(s => s.ServiceID == fallbackServiceId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync();
        return single ?? "Randevu";
    }

    private async Task ReplaceAppointmentServiceLinksAsync(int appointmentId, IReadOnlyList<int> orderedIds)
    {
        var existing = await _db.AppointmentServiceLinks
            .Where(l => l.AppointmentID == appointmentId)
            .ToListAsync();
        if (existing.Count > 0)
            _db.AppointmentServiceLinks.RemoveRange(existing);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            _db.AppointmentServiceLinks.Add(new AppointmentServiceLink
            {
                AppointmentID = appointmentId,
                ServiceID = orderedIds[i],
                SortOrder = i
            });
        }
        await _db.SaveChangesAsync();
    }

    private void EnsureTenantAuthorization(int entityTenantId)
    {
        var currentTenantId = _tenantProvider.GetTenantId();
        if (currentTenantId == null) return;

        if (currentTenantId.Value != entityTenantId)
        {
            _logger.LogWarning(
                "Cross-Tenant erişim engellendi! İstekTenant={RequestTenant}, HedefTenant={TargetTenant}",
                currentTenantId.Value, entityTenantId);
            throw new UnauthorizedAccessException("Bu kaynağa erişim yetkiniz bulunmamaktadır.");
        }
    }

    // ─── Slot Lock — In-Memory Distributed Lock ───────────────────────────────
    /// <summary>
    /// Aynı tenant + aynı saat için aynı anda sadece bir işleme izin verir.
    /// n8n kendi_sistemine_kaydet'ten ÖNCE bu lock'u alır.
    /// 30 saniye TTL — işlem tamamlanmazsa kilit otomatik düşer.
    /// </summary>
    private static string BuildLockKey(int tenantId, DateTime startDate)
        => $"slot_lock:{tenantId}:{startDate:yyyyMMddHHmm}";

    internal static bool IsActiveAppointmentStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return true;
        var s = status.ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        return !s.Contains("iptal") && !s.Contains("cancel");
    }

    public bool TryAcquireSlotLock(int tenantId, DateTime startDate, out string lockKey)
    {
        lockKey = BuildLockKey(tenantId, startDate);

        if (_cache.TryGetValue(lockKey, out _))
        {
            _logger.LogInformation("[SlotLock] Slot zaten işleniyor. Key={Key}", lockKey);
            return false;
        }

        _cache.Set(lockKey, true, TimeSpan.FromSeconds(30));
        _logger.LogInformation("[SlotLock] Kilit alındı. Key={Key}", lockKey);
        return true;
    }

    public void ReleaseSlotLock(string lockKey, int? expectedTenantId = null)
    {
        if (expectedTenantId.HasValue)
        {
            if (!SlotLockKeyParser.TryGetTenantId(lockKey, out var lockTenantId)
                || lockTenantId != expectedTenantId.Value)
            {
                throw new UnauthorizedAccessException("Bu kilit anahtarı işletme kapsamınız dışında.");
            }
        }

        _cache.Remove(lockKey);
        _logger.LogInformation("[SlotLock] Kilit bırakıldı. Key={Key}", lockKey);
    }

    // ─── Randevu Oluşturma ────────────────────────────────────────────────────
    public async Task<int> AddAppointmentAsync(AppointmentCreateDto dto)
    {
        if (dto.StartDate < DateTime.Now)
            throw new BadHttpRequestException("Geçmiş bir tarihe randevu verilemez.");

        dto.CustomerName = HtmlInputSanitizer.SanitizeName(dto.CustomerName);
        dto.CustomerPhone = HtmlInputSanitizer.SanitizePhone(dto.CustomerPhone);
        if (!string.IsNullOrWhiteSpace(dto.Note))
            dto.Note = HtmlInputSanitizer.SanitizeText(dto.Note);

        EnsureTenantAuthorization(dto.TenantID);

        var tenant = await _tenantRepository.Where(t => t.TenantID == dto.TenantID)
            .Include(t => t.BusinessHours)
            .Include(t => t.Holidays)
            .FirstOrDefaultAsync();

        // ── Çalışma Saati Kontrolü ───────────────────────────────────────────
        if (tenant?.BusinessHours != null && tenant.BusinessHours.Any())
        {
            int dayOfWeek = (int)dto.StartDate.DayOfWeek;
            var businessHour = tenant.BusinessHours.FirstOrDefault(b => b.DayOfWeek == dayOfWeek);

            if (businessHour != null)
            {
                if (businessHour.IsClosed)
                    throw new BadHttpRequestException("İşletme bu gün hizmet vermemektedir.");

                var timeOfDay = dto.StartDate.TimeOfDay;
                var endOfDay = dto.EndDate.TimeOfDay;

                if (timeOfDay < businessHour.OpenTime || endOfDay > businessHour.CloseTime)
                    throw new BadHttpRequestException(
                        $"Randevu saati işletmenin çalışma saatleri " +
                        $"({businessHour.OpenTime:hh\\:mm} - {businessHour.CloseTime:hh\\:mm}) dışındadır.");
            }
        }

        if (tenant != null && TenantBreakTimeHelper.OverlapsBreak(
                tenant.BreakTimeEnabled, tenant.BreakStartTime, tenant.BreakEndTime, dto.StartDate, dto.EndDate))
        {
            throw new BadHttpRequestException(
                $"Seçilen saat dilimi mola saatleri ({tenant.BreakStartTime:hh\\:mm} - {tenant.BreakEndTime:hh\\:mm}) ile çakışıyor.");
        }

        if (tenant != null)
        {
            var holiday = TenantHolidayHelper.FindHolidayForAppointment(tenant.Holidays, dto.StartDate, dto.EndDate);
            if (holiday != null)
                throw new BadHttpRequestException($"Bu gün {holiday.Name} nedeniyle randevu verilemez.");
        }

        if (!dto.AppUserID.HasValue || dto.AppUserID.Value <= 0)
            throw new BadHttpRequestException("Randevu için personel (AppUserID) seçilmelidir.");

        await EnsureStaffGoogleCalendarConnectedAsync(dto.AppUserID.Value);

        var orderedServiceIds = ResolveOrderedServiceIds(dto);
        if (orderedServiceIds.Count > 0)
        {
            dto.ServiceID = orderedServiceIds[0];
            dto.ServiceIds = orderedServiceIds;
        }

        var appointment = _mapper.Map<Appointment>(dto);
        appointment.Status = "Beklemede";
        appointment.Note = dto.Note ?? "Not eklenmedi";

        // ── Transaction + RepeatableRead + Kontrol İçeride ───────────────────
        // RepeatableRead: transaction boyunca okunan satırları kilitler.
        // Kontrol artık transaction içinde → race condition yok.
        // Son savunma hattı: DB Unique Index → DbUpdateException yakalar.
        if (_db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead);
            try
            {
                // ✅ Slot kontrolü transaction içinde
                bool isAvailable = await IsSlotAvailableAsync(dto.TenantID, dto.AppUserID ?? 0, dto.StartDate, dto.EndDate);
                if (!isAvailable)
                    throw new BadHttpRequestException("Seçilen saat dilimi başka bir randevu ile çakışıyor.");

                await _appointmentRepository.AddAsync(appointment);
                await _appointmentRepository.SaveAsync();

                if (orderedServiceIds.Count > 0)
                {
                    for (var i = 0; i < orderedServiceIds.Count; i++)
                    {
                        _db.AppointmentServiceLinks.Add(new AppointmentServiceLink
                        {
                            AppointmentID = appointment.AppointmentID,
                            ServiceID = orderedServiceIds[i],
                            SortOrder = i
                        });
                    }
                    await _db.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch (BadHttpRequestException)
            {
                await transaction.RollbackAsync();
                throw;
            }
            catch (DbUpdateException ex)
            {
                // DB Unique Index son savunma hattı
                await transaction.RollbackAsync();
                _logger.LogWarning(ex,
                    "[Çakışma] Unique Index ihlali: TenantID={TenantID}, Start={Start}",
                    dto.TenantID, dto.StartDate);
                throw new BadHttpRequestException("Seçilen saat dilimi başka bir randevu ile çakışıyor.");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            // In-Memory test ortamı
            bool isAvailable = await IsSlotAvailableAsync(dto.TenantID, dto.AppUserID ?? 0, dto.StartDate, dto.EndDate);
            if (!isAvailable)
                throw new BadHttpRequestException("Seçilen saat dilimi başka bir randevu ile çakışıyor.");

            await _appointmentRepository.AddAsync(appointment);
            await _appointmentRepository.SaveAsync();

            if (orderedServiceIds.Count > 0)
            {
                for (var i = 0; i < orderedServiceIds.Count; i++)
                {
                    _db.AppointmentServiceLinks.Add(new AppointmentServiceLink
                    {
                        AppointmentID = appointment.AppointmentID,
                        ServiceID = orderedServiceIds[i],
                        SortOrder = i
                    });
                }
                await _db.SaveChangesAsync();
            }
        }

        // ── Google Takvim (zorunlu) — başarısızsa DB kaydı geri alınır ─────────
        string? googleEventId;
        try
        {
            var summary = $"{dto.CustomerName} - {await FormatServiceNamesByIdsAsync(orderedServiceIds)}";
            var description = $"Telefon: {dto.CustomerPhone}";
            googleEventId = await _googleCalendarService.AddEventAsync(
                dto.AppUserID.Value, summary, description, dto.StartDate, dto.EndDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GoogleCalendar] Takvime eklenemedi. RandevuID={ID}", appointment.AppointmentID);
            await RollbackCreatedAppointmentAsync(appointment.AppointmentID);
            throw new BadHttpRequestException(
                "Randevu Google Takvime yazılamadı; kayıt oluşturulmadı. Personelin Google bağlantısını panelden kontrol edin.");
        }

        if (string.IsNullOrWhiteSpace(googleEventId))
        {
            _logger.LogError(
                "[GoogleCalendar] Event ID dönmedi. RandevuID={ID} AppUserID={StaffId}",
                appointment.AppointmentID, dto.AppUserID.Value);
            await RollbackCreatedAppointmentAsync(appointment.AppointmentID);
            throw new BadHttpRequestException(
                "Randevu Google Takvime yazılamadı; kayıt oluşturulmadı. Personelin Google Takvim bağlantısını yenileyin.");
        }

        await UpdateGoogleEventIdAsync(appointment.AppointmentID, googleEventId);

        // ── WhatsApp Bildirimi — transaction dışında, isteğe bağlı ───────────
        try
        {
            string instanceName = tenant?.InstanceName ?? $"tenant_{dto.TenantID}";
            var serviceLabel = await FormatServiceNamesByIdsAsync(orderedServiceIds);
            string message = $"Sayın {dto.CustomerName}, {dto.StartDate:dd.MM.yyyy HH:mm} tarihindeki ({serviceLabel}) randevunuz başarıyla oluşturulmuştur.";
            await _evolutionApiService.SendWhatsAppMessageAsync(instanceName, dto.CustomerPhone, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp bildirimi gönderilemedi: Randevu={AppointmentID}", appointment.AppointmentID);
        }

        return appointment.AppointmentID;
    }

    private async Task EnsureStaffGoogleCalendarConnectedAsync(int appUserId)
    {
        var staff = await _db.AppUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.AppUserID == appUserId);
        if (staff == null)
            throw new BadHttpRequestException("Seçilen personel bulunamadı.");

        if (string.IsNullOrWhiteSpace(staff.GoogleRefreshToken) || string.IsNullOrWhiteSpace(staff.GoogleCalendarId))
        {
            throw new BadHttpRequestException(
                $"{staff.FirstName} {staff.LastName} için Google Takvim bağlantısı eksik. " +
                "Panelden personeli Google ile yeniden bağlayın.");
        }
    }

    private async Task RollbackCreatedAppointmentAsync(int appointmentId)
    {
        try
        {
            var links = await _db.AppointmentServiceLinks
                .Where(l => l.AppointmentID == appointmentId)
                .ToListAsync();
            if (links.Count > 0)
                _db.AppointmentServiceLinks.RemoveRange(links);

            var apt = await _db.Appointments.FindAsync(appointmentId);
            if (apt != null)
                _db.Appointments.Remove(apt);
            else
            {
                var aptFromRepo = await _appointmentRepository
                    .Where(a => a.AppointmentID == appointmentId)
                    .FirstOrDefaultAsync();
                if (aptFromRepo != null)
                {
                    _appointmentRepository.Delete(aptFromRepo);
                    await _appointmentRepository.SaveAsync();
                }
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Rollback] Randevu geri alınamadı. AppointmentID={Id}", appointmentId);
        }
    }

    public async Task<List<Appointment>> GetAllByTenantIdAsync(int tenantId)
    {
        EnsureTenantAuthorization(tenantId);
        return await _appointmentRepository
            .Where(x => x.TenantID == tenantId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<AppointmentListItemDto>> GetListItemsByTenantIdAsync(int tenantId)
    {
        EnsureTenantAuthorization(tenantId);
        return await _appointmentRepository
            .Where(x => x.TenantID == tenantId)
            .AsNoTracking()
            .Select(a => new AppointmentListItemDto
            {
                AppointmentID = a.AppointmentID,
                CustomerName = a.CustomerName,
                CustomerPhone = a.CustomerPhone,
                StartDate = a.StartDate,
                EndDate = a.EndDate,
                Status = a.Status,
                ServiceID = a.ServiceID,
                AppUserID = a.AppUserID,
                GoogleEventID = a.GoogleEventID,
                IsConfirmed = a.IsConfirmed
            })
            .ToListAsync();
    }

    public async Task<Appointment?> GetByIdAsync(int id)
    {
        var appointment = await _appointmentRepository
            .Where(x => x.AppointmentID == id)
            .Include(a => a.Service)
            .Include(a => a.AppointmentServiceLinks).ThenInclude(l => l.Service)
            .FirstOrDefaultAsync();
        if (appointment != null)
            EnsureTenantAuthorization(appointment.TenantID);
        return appointment;
    }

    public async Task<bool> IsSlotAvailableAsync(int tenantId, int staffId, DateTime startDate, DateTime endDate)
    {
        var exists = await _appointmentRepository.Where(x =>
            x.TenantID == tenantId &&
            x.AppUserID == staffId &&
            x.StartDate < endDate &&
            x.EndDate > startDate).AsNoTracking().AnyAsync();
        return !exists;
    }

    public async Task<List<string>> GetAvailableSlotsAsync(int tenantId, DateTime targetDate, int durationMinutes, int count = 3)
    {
        var date = targetDate.Date;
        var tenantBreak = await _db.Tenants.AsNoTracking()
            .Where(t => t.TenantID == tenantId)
            .Select(t => new { t.BreakTimeEnabled, t.BreakStartTime, t.BreakEndTime })
            .FirstOrDefaultAsync();

        int dayOfWeek = (int)date.DayOfWeek;

        var businessHour = await _db.BusinessHours
            .Where(b => b.TenantID == tenantId && b.DayOfWeek == dayOfWeek)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (businessHour == null || businessHour.IsClosed)
            return new List<string>();

        var startOfDay = date.Add(businessHour.OpenTime);
        var endOfDay = date.Add(businessHour.CloseTime);

        var appointments = await _appointmentRepository
            .Where(x => x.TenantID == tenantId && x.StartDate >= startOfDay && x.StartDate < endOfDay)
            .AsNoTracking()
            .OrderBy(x => x.StartDate)
            .ToListAsync();

        var suggestions = new List<string>();

        var now = DateTime.Now;
        var currentTime = (targetDate.Date == now.Date && now > startOfDay)
            ? now.AddMinutes(15 - now.Minute % 15)
            : startOfDay;

        if (currentTime < startOfDay) currentTime = startOfDay;

        while (currentTime.AddMinutes(durationMinutes) <= endOfDay && suggestions.Count < count)
        {
            var potentialEnd = currentTime.AddMinutes(durationMinutes);

            if (tenantBreak != null)
            {
                var resumeAfterBreak = TenantBreakTimeHelper.GetResumeTimeAfterBreak(
                    tenantBreak.BreakTimeEnabled, tenantBreak.BreakStartTime, tenantBreak.BreakEndTime,
                    date, currentTime, potentialEnd);
                if (resumeAfterBreak != null)
                {
                    currentTime = resumeAfterBreak.Value;
                    continue;
                }
            }

            var conflictingAppt = appointments
                .Where(a => a.StartDate < potentialEnd && a.EndDate > currentTime)
                .OrderByDescending(a => a.EndDate)
                .FirstOrDefault();

            if (conflictingAppt != null)
            {
                currentTime = conflictingAppt.EndDate;
            }
            else
            {
                suggestions.Add(currentTime.ToString("HH:mm"));
                currentTime = currentTime.AddMinutes(durationMinutes);
            }
        }

        return suggestions;
    }

    public async Task UpdateAsync(Appointment appointment, int? previousAppUserID = null, IReadOnlyList<int>? orderedServiceIds = null)
    {
        EnsureTenantAuthorization(appointment.TenantID);

        if (appointment.StartDate < DateTime.Now)
            throw new BadHttpRequestException("Geçmiş bir tarihe randevu güncellenemez.");

        // ── Çalışma Saati Kontrolü (AddAppointmentAsync ile tutarlı) ──────────
        var tenant = await _tenantRepository.Where(t => t.TenantID == appointment.TenantID)
            .Include(t => t.BusinessHours)
            .Include(t => t.Holidays)
            .FirstOrDefaultAsync();

        if (tenant?.BusinessHours != null && tenant.BusinessHours.Any())
        {
            int dayOfWeek = (int)appointment.StartDate.DayOfWeek;
            var businessHour = tenant.BusinessHours.FirstOrDefault(b => b.DayOfWeek == dayOfWeek);

            if (businessHour != null)
            {
                if (businessHour.IsClosed)
                    throw new BadHttpRequestException("İşletme bu gün hizmet vermemektedir.");

                var timeOfDay = appointment.StartDate.TimeOfDay;
                var endOfDay = appointment.EndDate.TimeOfDay;

                if (timeOfDay < businessHour.OpenTime || endOfDay > businessHour.CloseTime)
                    throw new BadHttpRequestException(
                        $"Randevu saati işletmenin çalışma saatleri " +
                        $"({businessHour.OpenTime:hh\\:mm} - {businessHour.CloseTime:hh\\:mm}) dışındadır.");
            }
        }

        if (tenant != null && TenantBreakTimeHelper.OverlapsBreak(
                tenant.BreakTimeEnabled, tenant.BreakStartTime, tenant.BreakEndTime,
                appointment.StartDate, appointment.EndDate))
        {
            throw new BadHttpRequestException(
                $"Seçilen saat dilimi mola saatleri ({tenant.BreakStartTime:hh\\:mm} - {tenant.BreakEndTime:hh\\:mm}) ile çakışıyor.");
        }

        if (tenant != null)
        {
            var holiday = TenantHolidayHelper.FindHolidayForAppointment(tenant.Holidays, appointment.StartDate, appointment.EndDate);
            if (holiday != null)
                throw new BadHttpRequestException($"Bu gün {holiday.Name} nedeniyle randevu verilemez.");
        }

        // ── Slot Çakışma Kontrolü (kendisini hariç tutarak) ──────────────────
        var hasConflict = await _appointmentRepository.Where(x =>
                x.AppointmentID != appointment.AppointmentID &&
                x.TenantID == appointment.TenantID &&
                x.AppUserID == appointment.AppUserID &&
                x.StartDate < appointment.EndDate &&
                x.EndDate > appointment.StartDate)
            .AsNoTracking()
            .AnyAsync();
        if (hasConflict)
            throw new BadHttpRequestException("Seçilen saat dilimi başka bir randevu ile çakışıyor.");

        if (orderedServiceIds != null && orderedServiceIds.Count > 0)
            await ReplaceAppointmentServiceLinksAsync(appointment.AppointmentID, orderedServiceIds);

        // Google Takvim senkronizasyonu
        try
        {
            var serviceNames = await GetDisplayedServiceNamesAsync(appointment.AppointmentID, appointment.ServiceID);
            var summary = $"{appointment.CustomerName} - {serviceNames}";
            var description = $"Telefon: {appointment.CustomerPhone}";

            bool staffChanged = previousAppUserID.HasValue
                                && previousAppUserID.Value > 0
                                && previousAppUserID.Value != appointment.AppUserID;

            if (staffChanged)
            {
                var previousStaffId = previousAppUserID!.Value;
                var previousGoogleEventId = appointment.GoogleEventID;

                _logger.LogInformation(
                    "[GoogleCalendar] Personel değişti ({OldId}→{NewId}). RandevuID={ID}",
                    previousStaffId, appointment.AppUserID, appointment.AppointmentID);

                // Önce yeni personelin takvimine ekle; başarılı olursa eskiyi sil (tersi: silinip eklenemezse GoogleEventID null kalırdı)
                var newEventId = await _googleCalendarService.AddEventAsync(
                    appointment.AppUserID, summary, description, appointment.StartDate, appointment.EndDate);

                if (!string.IsNullOrEmpty(newEventId))
                {
                    appointment.GoogleEventID = newEventId;
                    if (!string.IsNullOrWhiteSpace(previousGoogleEventId))
                        await _googleCalendarService.DeleteEventAsync(previousStaffId, previousGoogleEventId);
                }
                else
                {
                    _logger.LogWarning(
                        "[GoogleCalendar] Personel değişiminde yeni takvime eklenemedi; eski event korunuyor. RandevuID={ID} GoogleEventID={EventId}",
                        appointment.AppointmentID, previousGoogleEventId ?? "(yok)");
                    // GoogleEventID ve eski takvimdeki event silinmez
                }
            }
            else if (!string.IsNullOrWhiteSpace(appointment.GoogleEventID))
            {
                var updated = await _googleCalendarService.UpdateEventAsync(
                    appointment.AppUserID, appointment.GoogleEventID, summary, description, appointment.StartDate, appointment.EndDate);

                if (!updated && appointment.AppUserID > 0)
                {
                    _logger.LogWarning(
                        "[GoogleCalendar] Event güncellenemedi, yeniden ekleniyor. RandevuID={ID}",
                        appointment.AppointmentID);
                    var newEventId = await _googleCalendarService.AddEventAsync(
                        appointment.AppUserID, summary, description, appointment.StartDate, appointment.EndDate);
                    if (!string.IsNullOrEmpty(newEventId))
                        appointment.GoogleEventID = newEventId;
                }
            }
            else if (appointment.AppUserID > 0)
            {
                var newEventId = await _googleCalendarService.AddEventAsync(
                    appointment.AppUserID, summary, description, appointment.StartDate, appointment.EndDate);

                if (!string.IsNullOrEmpty(newEventId))
                    appointment.GoogleEventID = newEventId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GoogleCalendar] Güncelleme sırasında takvim hatası. RandevuID={ID}", appointment.AppointmentID);
        }

        _appointmentRepository.Update(appointment);
        await _appointmentRepository.SaveAsync();
    }

    public async Task DeleteAsync(Appointment appointment)
    {
        // Google Takvim'den sil
        if (!string.IsNullOrWhiteSpace(appointment.GoogleEventID))
        {
            try
            {
                await _googleCalendarService.DeleteEventAsync(appointment.AppUserID, appointment.GoogleEventID);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GoogleCalendar] Takvimden silinemedi. RandevuID={ID}", appointment.AppointmentID);
            }
        }
        EnsureTenantAuthorization(appointment.TenantID);
        _appointmentRepository.Delete(appointment);
        await _appointmentRepository.SaveAsync();
    }

    public async Task<bool> UpdateGoogleEventIdAsync(int appointmentId, string googleEventId)
    {
        var appointment = await _db.Appointments.FindAsync(appointmentId);
        if (appointment == null) return false;

        appointment.GoogleEventID = googleEventId;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int?> GetStaffWithFewestAppointmentsAsync(int tenantId, DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        var todayAppointments = await _db.Appointments
            .Where(x => x.TenantID == tenantId &&
                        x.StartDate >= startOfDay &&
                        x.StartDate < endOfDay &&
                        x.AppUserID > 0)
            .AsNoTracking()
            .ToListAsync();

        var loadMap = todayAppointments
            .GroupBy(a => a.AppUserID)
            .ToDictionary(g => g.Key, g => g.Count());

        var activeStaff = await _db.AppUsers
            .Where(u => u.TenantID == tenantId && u.Status)
            .AsNoTracking()
            .ToListAsync();

        if (!activeStaff.Any()) return null;

        return activeStaff
            .OrderBy(u => loadMap.TryGetValue(u.AppUserID, out var cnt) ? cnt : 0)
            .ThenBy(u => u.AppUserID)
            .First().AppUserID;
    }

    public async Task<CustomerHistoryDto> GetCustomerHistoryAsync(string phoneNumber, int tenantId)
    {
        var phoneKeys = AppointmentPhoneNormalizer.BuildLookupKeys(phoneNumber);

        var history = await _db.Appointments
            .Where(a => a.TenantID == tenantId && phoneKeys.Contains(a.CustomerPhone))
            .Include(a => a.Service)
            .Include(a => a.AppointmentServiceLinks).ThenInclude(l => l.Service)
            .OrderByDescending(a => a.StartDate)
            .AsNoTracking()
            .ToListAsync();

        if (!history.Any())
            return new CustomerHistoryDto
            {
                IsReturningCustomer = false,
                TotalVisits = 0,
                SummaryForAI = "Yeni müşteri. Daha önce bu işletmeye gelmemiş."
            };

        var last = history.First();
        var totalVisits = history.Count;
        var isRegular = totalVisits >= 3;

        static string DisplayService(Appointment a)
        {
            if (a.AppointmentServiceLinks != null && a.AppointmentServiceLinks.Count > 0)
                return string.Join(", ", a.AppointmentServiceLinks.OrderBy(l => l.SortOrder).Select(l => l.Service?.Name).Where(n => n != null));
            return a.Service?.Name ?? "Bilinmiyor";
        }

        var lastServiceName = DisplayService(last);

        var summary = isRegular
            ? $"Düzenli müşteri ({totalVisits}. ziyaret). Son ziyaret: {last.StartDate:dd MMMM yyyy} - {lastServiceName}. Sıcak karşıla, adıyla hitap et."
            : $"Daha önce gelmiş müşteri ({totalVisits}. ziyaret). Son ziyaret: {last.StartDate:dd MMMM yyyy} - {lastServiceName}.";

        return new CustomerHistoryDto
        {
            IsReturningCustomer = true,
            TotalVisits = totalVisits,
            CustomerName = last.CustomerName,
            LastVisitDate = last.StartDate,
            LastServiceName = lastServiceName,
            LastVisitStatus = last.Status,
            SummaryForAI = summary
        };
    }

    public async Task<List<Appointment>> GetTomorrowAppointmentsAsync(int tenantId)
    {
        var tomorrow = DateTime.Today.AddDays(1);
        var dayAfter = tomorrow.AddDays(1);

        return await _db.Appointments
            .Where(a => a.TenantID == tenantId &&
                        a.StartDate >= tomorrow &&
                        a.StartDate < dayAfter)
            .Include(a => a.Service)
            .Include(a => a.AppointmentServiceLinks).ThenInclude(l => l.Service)
            .AsNoTracking()
            .OrderBy(a => a.StartDate)
            .ToListAsync();
    }

    public async Task<List<AppointmentReminderPendingDto>> GetPendingRemindersAsync()
    {
        var tomorrow = DateTime.Today.AddDays(1);
        var dayAfter = tomorrow.AddDays(1);

        var appointments = await _db.Appointments
            .Where(a => a.StartDate >= tomorrow &&
                        a.StartDate < dayAfter &&
                        a.ReminderSentAt == null &&
                        !string.IsNullOrWhiteSpace(a.CustomerPhone) &&
                        (a.Status == null || !a.Status.ToLower().Contains("iptal")))
            .Include(a => a.Tenant)
            .Include(a => a.Service)
            .Include(a => a.AppointmentServiceLinks).ThenInclude(l => l.Service)
            .AsNoTracking()
            .OrderBy(a => a.TenantID)
            .ThenBy(a => a.StartDate)
            .ToListAsync();

        var result = new List<AppointmentReminderPendingDto>();
        foreach (var a in appointments)
        {
            if (a.Tenant == null || !PlanPricing.CanUseReminders(a.Tenant.PlanType))
                continue;
            if (!a.Tenant.IsActive || !a.Tenant.IsSubscriptionActive || a.Tenant.IsBlacklisted)
                continue;
            if (string.IsNullOrWhiteSpace(a.Tenant.InstanceName))
                continue;

            var serviceName = a.AppointmentServiceLinks != null && a.AppointmentServiceLinks.Count > 0
                ? string.Join(", ", a.AppointmentServiceLinks.OrderBy(l => l.SortOrder).Select(l => l.Service?.Name).Where(n => n != null))
                : a.Service?.Name ?? "Randevu";

            var startLocal = a.StartDate.ToString("dd.MM.yyyy HH:mm");
            var shopName = a.Tenant.Name;
            var message =
                $"Merhaba {a.CustomerName}, {shopName} randevunuzu hatırlatmak isteriz: Yarın {startLocal} — {serviceName}. " +
                "Gelemeyecekseniz lütfen bizi bilgilendirin. İyi günler dileriz.";

            result.Add(new AppointmentReminderPendingDto
            {
                AppointmentId = a.AppointmentID,
                TenantId = a.TenantID,
                ShopName = shopName,
                InstanceName = a.Tenant.InstanceName!,
                CustomerName = a.CustomerName,
                CustomerPhone = a.CustomerPhone,
                StartDate = startLocal,
                ServiceName = serviceName,
                MessageText = message
            });
        }

        return result;
    }

    public async Task<AppointmentReminderRunResultDto> SendPendingRemindersAsync()
    {
        var pending = await GetPendingRemindersAsync();
        var result = new AppointmentReminderRunResultDto { Total = pending.Count };

        foreach (var job in pending)
        {
            try
            {
                var sent = await _evolutionApiService.SendWhatsAppMessageAsync(
                    job.InstanceName, job.CustomerPhone, job.MessageText);

                if (!sent)
                {
                    result.Failed++;
                    result.Errors.Add($"AppointmentId={job.AppointmentId}: WhatsApp gönderilemedi.");
                    continue;
                }

                var appointment = await _db.Appointments
                    .FirstOrDefaultAsync(x => x.AppointmentID == job.AppointmentId);
                if (appointment != null)
                {
                    appointment.ReminderSentAt = DateTime.UtcNow;
                    _db.Appointments.Update(appointment);
                    await _db.SaveChangesAsync();
                }

                result.Sent++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"AppointmentId={job.AppointmentId}: {ex.Message}");
                _logger.LogError(ex, "Hatırlatma gönderilemedi. AppointmentId={Id}", job.AppointmentId);
            }
        }

        return result;
    }
    public async Task<List<string>> GetAvailableSlotsByStaffAsync(int tenantId, int staffId, DateTime targetDate, int durationMinutes, int count = 100, string? requestedTime = null)
    {
        var date = targetDate.Date;
        var dateOnly = DateOnly.FromDateTime(date);
        if (await _db.Holidays.AsNoTracking().AnyAsync(h => h.TenantId == tenantId && h.Date == dateOnly))
            return new List<string>();

        var tenantBreak = await _db.Tenants.AsNoTracking()
            .Where(t => t.TenantID == tenantId)
            .Select(t => new { t.BreakTimeEnabled, t.BreakStartTime, t.BreakEndTime })
            .FirstOrDefaultAsync();

        int dayOfWeek = (int)date.DayOfWeek;
        var businessHour = await _db.BusinessHours
            .Where(b => b.TenantID == tenantId && b.DayOfWeek == dayOfWeek)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (businessHour == null || businessHour.IsClosed)
            return new List<string>();
        var startOfDay = date.Add(businessHour.OpenTime);
        var endOfDay = date.Add(businessHour.CloseTime);

        var appointments = await _appointmentRepository
            .Where(x => x.TenantID == tenantId
                     && x.AppUserID == staffId
                     && x.StartDate >= startOfDay
                     && x.StartDate < endOfDay)
            .AsNoTracking()
            .OrderBy(x => x.StartDate)
            .ToListAsync();

        // Belirli bir saat istendiyse sadece onu kontrol et
        if (!string.IsNullOrEmpty(requestedTime) && TimeSpan.TryParse(requestedTime, out var reqSpan))
        {
            var requestedStart = date.Add(reqSpan);
            var requestedEnd = requestedStart.AddMinutes(durationMinutes);

            if (requestedStart >= startOfDay && requestedEnd <= endOfDay)
            {
                if (tenantBreak != null)
                {
                    var resume = TenantBreakTimeHelper.GetResumeTimeAfterBreak(
                        tenantBreak.BreakTimeEnabled, tenantBreak.BreakStartTime, tenantBreak.BreakEndTime,
                        date, requestedStart, requestedEnd);
                    if (resume != null)
                        return new List<string>();
                }

                var hasConflict = appointments.Any(a => a.StartDate < requestedEnd && a.EndDate > requestedStart);
                if (!hasConflict)
                    return new List<string> { requestedStart.ToString("HH:mm") };
                else
                    return new List<string>(); // çakışma var, boş liste döner
            }
            return new List<string>(); // çalışma saati dışında
        }

        var suggestions = new List<string>();
        var now = DateTime.Now;
        var currentTime = (date == now.Date && now > startOfDay)
            ? now.AddMinutes(15 - now.Minute % 15)
            : startOfDay;
        if (currentTime < startOfDay) currentTime = startOfDay;

        while (currentTime.AddMinutes(durationMinutes) <= endOfDay && suggestions.Count < count)
        {
            var potentialEnd = currentTime.AddMinutes(durationMinutes);
            if (tenantBreak != null)
            {
                var resumeAfterBreak = TenantBreakTimeHelper.GetResumeTimeAfterBreak(
                    tenantBreak.BreakTimeEnabled, tenantBreak.BreakStartTime, tenantBreak.BreakEndTime,
                    date, currentTime, potentialEnd);
                if (resumeAfterBreak != null)
                {
                    currentTime = resumeAfterBreak.Value;
                    continue;
                }
            }

            var conflict = appointments
                .Where(a => a.StartDate < potentialEnd && a.EndDate > currentTime)
                .OrderByDescending(a => a.EndDate)
                .FirstOrDefault();
            if (conflict != null)
                currentTime = conflict.EndDate;
            else
            {
                suggestions.Add(currentTime.ToString("HH:mm"));
                currentTime = currentTime.AddMinutes(durationMinutes);
            }
        }

        // En geç başlangıç saatini de kontrol et
        var latestStart = endOfDay.AddMinutes(-durationMinutes);
        if (!suggestions.Contains(latestStart.ToString("HH:mm")))
        {
            var latestEnd = latestStart.AddMinutes(durationMinutes);
            var blockedByBreak = tenantBreak != null && TenantBreakTimeHelper.GetResumeTimeAfterBreak(
                tenantBreak.BreakTimeEnabled, tenantBreak.BreakStartTime, tenantBreak.BreakEndTime,
                date, latestStart, latestEnd) != null;
            var hasConflict = appointments.Any(a => a.StartDate < latestEnd && a.EndDate > latestStart);
            if (!blockedByBreak && !hasConflict && latestStart >= now)
                suggestions.Add(latestStart.ToString("HH:mm"));
        }

        return suggestions;
    }
    public async Task<List<object>> GetAvailableSlotsForAllStaffAsync(int tenantId, DateTime targetDate, int durationMinutes, int count = 100)
    {
        var activeStaff = await _db.AppUsers
            .Where(u => u.TenantID == tenantId && u.Status)
            .AsNoTracking()
            .ToListAsync();

        var result = new List<object>();

        foreach (var staff in activeStaff)
        {
            var slots = await GetAvailableSlotsByStaffAsync(tenantId, staff.AppUserID, targetDate, durationMinutes, count);
            if (slots.Any())
            {
                result.Add(new
                {
                    StaffId = staff.AppUserID,
                    StaffName = staff.FirstName + " " + staff.LastName,
                    AvailableSlots = slots
                });
            }
        }

        return result;
    }

    public async Task<List<object>> GetActiveAppointmentsByPhoneAsync(string phone, int tenantId)
    {
        var phoneKeys = AppointmentPhoneNormalizer.BuildLookupKeys(phone);

        var appointments = await _db.Appointments
            .Where(a => a.TenantID == tenantId &&
                       a.StartDate >= DateTime.Now &&
                       phoneKeys.Contains(a.CustomerPhone))
            .Include(a => a.Service)
            .Include(a => a.AppointmentServiceLinks).ThenInclude(l => l.Service)
            .OrderBy(a => a.StartDate)
            .AsNoTracking()
            .ToListAsync();

        appointments = appointments.Where(a => IsActiveAppointmentStatus(a.Status)).ToList();

        static string DisplayService(Appointment a)
        {
            if (a.AppointmentServiceLinks != null && a.AppointmentServiceLinks.Count > 0)
                return string.Join(", ", a.AppointmentServiceLinks.OrderBy(l => l.SortOrder).Select(l => l.Service?.Name).Where(n => n != null));
            return a.Service?.Name ?? "Bilinmiyor";
        }

        return appointments.Select(a => (object)new
        {
            AppointmentID = a.AppointmentID,
            StartDate = a.StartDate.ToString("dd.MM.yyyy HH:mm"),
            EndDate = a.EndDate.ToString("dd.MM.yyyy HH:mm"),
            ServiceName = DisplayService(a),
            Status = a.Status
        }).ToList();
    }

    public async Task<List<CustomerAppointmentDto>> GetActiveAppointmentsForCustomerAsync(int tenantId, string phoneOrJid)
    {
        var keys = AppointmentPhoneNormalizer.BuildLookupKeys(phoneOrJid);
        if (keys.Count == 0)
            return new List<CustomerAppointmentDto>();

        var rows = await _appointmentRepository.GetActiveByPhoneAsync(
            tenantId, keys is IReadOnlyCollection<string> rc ? rc : keys.ToList(), DateTime.Now);

        static string? FormatServiceName(Appointment a)
        {
            if (a.AppointmentServiceLinks != null && a.AppointmentServiceLinks.Count > 0)
            {
                var joined = string.Join(", ", a.AppointmentServiceLinks
                    .OrderBy(l => l.SortOrder)
                    .Select(l => l.Service?.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n)));
                if (!string.IsNullOrWhiteSpace(joined))
                    return joined;
            }
            return a.Service?.Name;
        }

        static string? FormatStaffName(Appointment a)
        {
            if (a.AppUser == null)
                return null;
            var full = $"{a.AppUser.FirstName} {a.AppUser.LastName}".Trim();
            return string.IsNullOrWhiteSpace(full) ? null : full;
        }

        return rows
            .Where(a => IsActiveAppointmentStatus(a.Status))
            .Select(a => new CustomerAppointmentDto
            {
                AppointmentId = a.AppointmentID,
                CustomerName = a.CustomerName,
                CustomerPhone = a.CustomerPhone,
                StartTime = a.StartDate,
                EndTime = a.EndDate,
                StaffName = FormatStaffName(a),
                ServiceName = FormatServiceName(a),
                Status = a.Status
            })
            .ToList();
    }
}