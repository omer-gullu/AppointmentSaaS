using System;
using System.Threading.Tasks;

namespace Appointment_SaaS.WebUI.Services.Abstract
{
    public interface IGoogleCalendarApiService
    {
        Task<string?> CreateEventAsync(string instanceName, string customerName, int serviceId, string customerPhone, DateTime startDate, int? staffId = null);
        Task<bool> UpdateEventAsync(string instanceName, string googleEventId, string customerName, int serviceId, string customerPhone, DateTime startDate, int? staffId = null);
        Task<bool> DeleteEventAsync(string instanceName, string googleEventId, int? staffId = null);
        string GetConnectUrl();
        string GetConnectStaffUrl(int staffId);
        Task<(bool Success, string Message)> ProcessCallbackAsync(string code, int tenantId);
        Task<(bool Success, string Message)> ProcessStaffCallbackAsync(string code, int tenantId, int staffId);
    }
}
