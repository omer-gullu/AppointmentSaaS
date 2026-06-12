namespace Appointment_SaaS.Core.DTOs;

/// <summary>
/// WhatsApp/n8n tarafında müşterinin tek satırlık aktif randevu bilgisi.
/// </summary>
public class CustomerAppointmentDto
{
    public int AppointmentId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? StaffName { get; set; }
    public string? ServiceName { get; set; }
    public string? Status { get; set; }
}
