using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
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

    public AppointmentManager(
        IAppointmentRepository appointmentRepository,
        IMapper mapper,
        ITenantRepository tenantRepository,
        IEvolutionApiService evolutionApiService,
        AppDbContext db,
        ITenantProvider tenantProvider,
        ILogger<AppointmentManager> logger,
        IMemoryCache cache)
    {
        _appointmentRepository = appointmentRepository;
        _mapper = mapper;
        _tenantRepository = tenantRepository;
        _evolutionApiService = evolutionApiService;
        _db = db;
        _tenantProvider = tenantProvider;
        _logger = logger;
        _cache = cache;
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

    public void ReleaseSlotLock(string lockKey)
    {
        _cache.Remove(lockKey);
        _logger.LogInformation("[SlotLock] Kilit bırakıldı. Key={Key}", lockKey);
    }

    // ─── Randevu Oluşturma ────────────────────────────────────────────────────
    public async Task<int> AddAppointmentAsync(AppointmentCreateDto dto)
    {
        if (dto.StartDate < DateTime.Now)
            throw new BadHttpRequestException("Geçmiş bir tarihe randevu verilemez.");

        EnsureTenantAuthorization(dto.TenantID);

        var tenant = await _tenantRepository.Where(t => t.TenantID == dto.TenantID)
            .Include(t => t.BusinessHours)
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
                bool isAvailable = await IsSlotAvailableAsync(dto.TenantID, dto.StartDate, dto.EndDate);
                if (!isAvailable)
                    throw new BadHttpRequestException("Seçilen saat dilimi başka bir randevu ile çakışıyor.");

                await _appointmentRepository.AddAsync(appointment);
                await _appointmentRepository.SaveAsync();
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
            bool isAvailable = await IsSlotAvailableAsync(dto.TenantID, dto.StartDate, dto.EndDate);
            if (!isAvailable)
                throw new BadHttpRequestException("Seçilen saat dilimi başka bir randevu ile çakışıyor.");

            await _appointmentRepository.AddAsync(appointment);
            await _appointmentRepository.SaveAsync();
        }

        // ── WhatsApp Bildirimi — transaction dışında ──────────────────────────
        try
        {
            string instanceName = tenant?.InstanceName ?? $"tenant_{dto.TenantID}";
            string message = $"Sayın {dto.CustomerName}, {dto.StartDate:dd.MM.yyyy HH:mm} tarihindeki randevunuz başarıyla oluşturulmuştur.";
            await _evolutionApiService.SendWhatsAppMessageAsync(instanceName, dto.CustomerPhone, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp bildirimi gönderilemedi: Randevu={AppointmentID}", appointment.AppointmentID);
        }

        return appointment.AppointmentID;
    }

    public async Task<List<Appointment>> GetAllByTenantIdAsync(int tenantId)
    {
        EnsureTenantAuthorization(tenantId);
        return await _appointmentRepository.Where(x => x.TenantID == tenantId).AsNoTracking().ToListAsync();
    }

    public async Task<Appointment?> GetByIdAsync(int id)
    {
        var appointment = await _appointmentRepository.Where(x => x.AppointmentID == id).FirstOrDefaultAsync();
        if (appointment != null)
            EnsureTenantAuthorization(appointment.TenantID);
        return appointment;
    }

    public async Task<bool> IsSlotAvailableAsync(int tenantId, DateTime startDate, DateTime endDate)
    {
        var exists = await _appointmentRepository.Where(x =>
            x.TenantID == tenantId &&
            x.StartDate < endDate &&
            x.EndDate > startDate).AsNoTracking().AnyAsync();
        return !exists;
    }

    public async Task<List<string>> GetAvailableSlotsAsync(int tenantId, DateTime targetDate, int durationMinutes, int count = 3)
    {
        var date = targetDate.Date;
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

    public async Task UpdateAsync(Appointment appointment)
    {
        EnsureTenantAuthorization(appointment.TenantID);
        _appointmentRepository.Update(appointment);
        await _appointmentRepository.SaveAsync();
    }

    public async Task DeleteAsync(Appointment appointment)
    {
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
        static string Normalize(string p)
        {
            p = p.Trim();
            p = new string(p.Where(char.IsDigit).ToArray());
            if (p.StartsWith("90") && p.Length > 10) p = p[2..];
            if (p.StartsWith("0") && p.Length > 10) p = p[1..];
            return p;
        }

        var normalized = Normalize(phoneNumber);

        var history = await _db.Appointments
            .Where(a => a.TenantID == tenantId &&
                       (a.CustomerPhone == normalized ||
                        a.CustomerPhone == "0" + normalized ||
                        a.CustomerPhone == "90" + normalized ||
                        a.CustomerPhone == "+90" + normalized))
            .Include(a => a.Service)
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

        var summary = isRegular
            ? $"Düzenli müşteri ({totalVisits}. ziyaret). Son ziyaret: {last.StartDate:dd MMMM yyyy} - {last.Service?.Name ?? "Bilinmiyor"}. Sıcak karşıla, adıyla hitap et."
            : $"Daha önce gelmiş müşteri ({totalVisits}. ziyaret). Son ziyaret: {last.StartDate:dd MMMM yyyy} - {last.Service?.Name ?? "Bilinmiyor"}.";

        return new CustomerHistoryDto
        {
            IsReturningCustomer = true,
            TotalVisits = totalVisits,
            CustomerName = last.CustomerName,
            LastVisitDate = last.StartDate,
            LastServiceName = last.Service?.Name,
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
            .AsNoTracking()
            .OrderBy(a => a.StartDate)
            .ToListAsync();
    }
}