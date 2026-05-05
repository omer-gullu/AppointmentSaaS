using System;
using System.Threading.Tasks;

namespace Appointment_SaaS.WebUI.Services.Abstract
{
    public interface IAppointmentApiService
    {
        Task<(bool Success, string Message, int? AppointmentId, int? AppUserId)> CreateAppointmentAsync(
            int tenantId, string customerName, string customerPhone, int serviceId, DateTime startDate);
        
        Task<(bool Success, string Message)> UpdateAppointmentAsync(
            int tenantId, int appointmentId, string customerName, string customerPhone, int serviceId, DateTime startDate, string? googleEventId);
        
        Task<(bool Success, string Message)> DeleteAppointmentAsync(int appointmentId);
        
        // This helper might be needed if updating an appointment's googleEventId after creation
        Task<bool> UpdateAppointmentGoogleEventIdAsync(int appointmentId, int tenantId, string customerName, string customerPhone, int serviceId, DateTime startDate, string googleEventId);
    }
}
