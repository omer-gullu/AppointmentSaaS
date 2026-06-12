using Appointment_SaaS.Core.DTOs;

namespace Appointment_SaaS.WebUI.Services.Abstract;

public interface IWhatsAppBlockedPhoneApiService
{
    Task<(List<TenantBlockedPhoneDto> Items, string? ErrorMessage)> GetListAsync();
    Task<(bool Success, string Message, TenantBlockedPhoneDto? Item)> AddAsync(string phone, string? note);
    Task<(bool Success, string Message)> DeleteAsync(int id);
}
