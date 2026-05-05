namespace Appointment_SaaS.Core.DTOs;

/// <summary>
/// n8n'den gelen Google Takvim Event ID'sini randevuya bağlamak için kullanılır.
/// PATCH /api/appointments/{id}/google-event endpoint'inde kullanılır.
/// </summary>
public class GoogleEventUpdateDto
{
    /// <summary>Google Calendar API'den dönen event ID (örn: "abc123xyz_20260419T100000")</summary>
    public string GoogleEventId { get; set; } = string.Empty;
}
