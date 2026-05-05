using Appointment_SaaS.WebUI.Models;
using System.Threading.Tasks;

namespace Appointment_SaaS.WebUI.Services.Abstract
{
    public interface IDashboardApiService
    {
        Task<DashboardViewModel> GetDashboardDataAsync(int tenantId);
        Task<bool> ToggleAssistantAsync(int tenantId, bool isActive);
        Task<bool> CreateServiceAsync(int tenantId, string name, decimal price, int durationMinutes);
        Task<bool> UpdateServiceAsync(int tenantId, int serviceId, string name, decimal price, int durationMinutes);
        Task<bool> DeleteServiceAsync(int serviceId);
        Task<(bool Success, string Message)> CancelSubscriptionAsync(int tenantId);
    }
}
