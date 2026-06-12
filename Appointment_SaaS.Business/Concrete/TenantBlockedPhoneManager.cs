using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Constants;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using Appointment_SaaS.Data.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Appointment_SaaS.Business.Concrete;

public class TenantBlockedPhoneManager : ITenantBlockedPhoneService
{
    private readonly ITenantBlockedPhoneRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly ILogger<TenantBlockedPhoneManager> _logger;

    public TenantBlockedPhoneManager(
        ITenantBlockedPhoneRepository repository,
        ITenantService tenantService,
        ILogger<TenantBlockedPhoneManager> logger)
    {
        _repository = repository;
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task<List<TenantBlockedPhoneDto>> GetByTenantAsync(int tenantId)
    {
        var rows = await _repository
            .Where(x => x.TenantID == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return rows.Select(MapToDto).ToList();
    }

    public async Task<TenantBlockedPhoneDto> AddManualAsync(int tenantId, TenantBlockedPhoneCreateDto dto)
    {
        var core = NormalizeOrThrow(dto.Phone);
        var entity = await UpsertAsync(tenantId, core, WhatsAppBlockedPhoneSources.Manual, dto.Note);
        await _repository.SaveAsync();
        return MapToDto(entity);
    }

    public async Task<bool> RemoveAsync(int tenantId, int blockedPhoneId)
    {
        var row = await _repository
            .Where(x => x.TenantBlockedPhoneID == blockedPhoneId && x.TenantID == tenantId)
            .FirstOrDefaultAsync();

        if (row == null)
            return false;

        _repository.Delete(row);
        await _repository.SaveAsync();
        return true;
    }

    public async Task<WhatsAppBlockedCheckDto> IsBlockedAsync(int tenantId, string phone)
    {
        var core = AppointmentPhoneNormalizer.NormalizeCore(phone);
        if (string.IsNullOrEmpty(core))
            return new WhatsAppBlockedCheckDto { Blocked = false, PhoneCore = string.Empty };

        var blocked = await _repository
            .Where(x => x.TenantID == tenantId && x.PhoneCore == core)
            .AnyAsync();

        return new WhatsAppBlockedCheckDto { Blocked = blocked, PhoneCore = core };
    }

    public async Task<WhatsAppBlockedCheckDto> OptOutAsync(WhatsAppOptOutDto dto)
    {
        var tenantId = await ResolveTenantIdAsync(dto.TenantId, dto.InstanceName);
        if (tenantId == null)
            throw new BadHttpRequestException("İşletme bulunamadı (tenantId veya instanceName geçersiz).");

        var core = NormalizeOrThrow(dto.Phone);
        var note = BuildOptOutNote(dto.CustomerName);
        await UpsertAsync(tenantId.Value, core, WhatsAppBlockedPhoneSources.SelfOptOut, note);
        await _repository.SaveAsync();

        _logger.LogInformation(
            "WhatsApp opt-out. TenantId={TenantId} PhoneCore={PhoneCore}",
            tenantId, core);

        return new WhatsAppBlockedCheckDto { Blocked = true, PhoneCore = core };
    }

    public async Task<int?> ResolveTenantIdAsync(int tenantId, string? instanceName)
    {
        if (tenantId > 0)
        {
            var byId = await _tenantService.GetByIdAsync(tenantId);
            return byId?.TenantID;
        }

        if (string.IsNullOrWhiteSpace(instanceName))
            return null;

        var byInstance = await _tenantService.GetContextByInstanceAsync(instanceName.Trim());
        return byInstance?.TenantID;
    }

    private async Task<TenantBlockedPhone> UpsertAsync(int tenantId, string phoneCore, string source, string? note)
    {
        var existing = await _repository
            .Where(x => x.TenantID == tenantId && x.PhoneCore == phoneCore)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.Source = source;
            if (!string.IsNullOrWhiteSpace(note))
                existing.Note = note;
            _repository.Update(existing);
            return existing;
        }

        var entity = new TenantBlockedPhone
        {
            TenantID = tenantId,
            PhoneCore = phoneCore,
            Source = source,
            Note = note,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(entity);
        return entity;
    }

    private static string BuildOptOutNote(string? customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            return "Asistanı kapat";
        return $"Asistanı kapat — {customerName.Trim()}";
    }

    private static string NormalizeOrThrow(string? phone)
    {
        var core = AppointmentPhoneNormalizer.NormalizeCore(phone);
        if (string.IsNullOrEmpty(core) || core.Length != 10 || core[0] != '5')
            throw new BadHttpRequestException("Geçerli bir cep telefonu numarası girin (örn. 05XXXXXXXXX).");
        return core;
    }

    private static TenantBlockedPhoneDto MapToDto(TenantBlockedPhone e) => new()
    {
        Id = e.TenantBlockedPhoneID,
        Phone = "0" + e.PhoneCore,
        Source = e.Source,
        Note = e.Note,
        CreatedAt = e.CreatedAt
    };
}
