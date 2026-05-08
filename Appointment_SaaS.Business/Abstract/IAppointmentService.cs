using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract;

public interface IAppointmentService
{
    Task<List<Appointment>> GetAllByTenantIdAsync(int tenantId);
    Task<Appointment?> GetByIdAsync(int id);
    Task<int> AddAppointmentAsync(AppointmentCreateDto dto);
    Task<bool> IsSlotAvailableAsync(int tenantId, int staffId, DateTime startDate, DateTime endDate);
    Task<List<string>> GetAvailableSlotsAsync(int tenantId, DateTime targetDate, int durationMinutes, int count = 3);
    Task UpdateAsync(Appointment appointment, int? previousAppUserID = null);
    Task DeleteAsync(Appointment appointment);

    /// <summary>Google Takvim event kaydedildikten sonra n8n'den gelen ID'yi DB'ye işle.</summary>
    Task<bool> UpdateGoogleEventIdAsync(int appointmentId, string googleEventId);

    /// <summary>Bugün en az randevusu olan aktif personeli döndür (Load-Balancing).</summary>
    Task<int?> GetStaffWithFewestAppointmentsAsync(int tenantId, DateTime date);

    /// <summary>Müşteri geçmişini döndürür. n8n AI Agent prompt'una eklenir.</summary>
    Task<CustomerHistoryDto> GetCustomerHistoryAsync(string phoneNumber, int tenantId);

    /// <summary>Yarınki randevuları döndürür. Hatırlatma workflow'u için.</summary>
    Task<List<Appointment>> GetTomorrowAppointmentsAsync(int tenantId);
    Task<List<object>> GetActiveAppointmentsByPhoneAsync(string phone, int tenantId);

    /// <summary>
    /// Slot için distributed lock alır.
    /// Aynı tenant + aynı saat için aynı anda sadece bir işlem çalışabilir.
    /// n8n kendi_sistemine_kaydet'ten önce bu lock'u alır.
    /// </summary>
    bool TryAcquireSlotLock(int tenantId, DateTime startDate, out string lockKey);

    /// <summary>Alınan slot kilidini serbest bırakır.</summary>
    void ReleaseSlotLock(string lockKey);
    Task<List<string>> GetAvailableSlotsByStaffAsync(int tenantId, int staffId, DateTime targetDate, int durationMinutes, int count = 100, string? requestedTime = null);
    Task<List<object>> GetAvailableSlotsForAllStaffAsync(int tenantId, DateTime targetDate, int durationMinutes, int count = 100);
}