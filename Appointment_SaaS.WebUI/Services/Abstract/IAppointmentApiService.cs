using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Appointment_SaaS.WebUI.Services.Abstract
{
    public interface IAppointmentApiService
    {
        /// <param name="serviceIds">Birden fazla hizmet sırası; null veya boşsa yalnızca <paramref name="serviceId"/> kullanılır.</param>
        Task<(bool Success, string Message, int? AppointmentId, int? AppUserId)> CreateAppointmentAsync(
            int tenantId, string customerName, string customerPhone, int serviceId, DateTime startDate, int? appUserId = null, IReadOnlyList<int>? serviceIds = null);

        /// <param name="serviceIds">Birden fazla hizmet sırası; null veya boşsa yalnızca <paramref name="serviceId"/> kullanılır.</param>
        Task<(bool Success, string Message)> UpdateAppointmentAsync(
            int tenantId, int appointmentId, string customerName, string customerPhone, int serviceId, DateTime startDate, string? googleEventId, int? appUserId = null, IReadOnlyList<int>? serviceIds = null);
        
        Task<(bool Success, string Message)> DeleteAppointmentAsync(int appointmentId);
        
        // This helper might be needed if updating an appointment's googleEventId after creation
        Task<bool> UpdateAppointmentGoogleEventIdAsync(int appointmentId, int tenantId, string customerName, string customerPhone, int serviceId, DateTime startDate, string googleEventId);
    }
}
