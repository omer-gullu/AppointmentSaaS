using Appointment_SaaS.Core.DTOs;

namespace Appointment_SaaS.Business.Abstract;

public interface ITenantBlockedPhoneService
{
    Task<List<TenantBlockedPhoneDto>> GetByTenantAsync(int tenantId);
    Task<TenantBlockedPhoneDto> AddManualAsync(int tenantId, TenantBlockedPhoneCreateDto dto);
    Task<bool> RemoveAsync(int tenantId, int blockedPhoneId);
    Task<WhatsAppBlockedCheckDto> IsBlockedAsync(int tenantId, string phone);
    Task<WhatsAppBlockedCheckDto> OptOutAsync(WhatsAppOptOutDto dto);
    Task<int?> ResolveTenantIdAsync(int tenantId, string? instanceName);
}
