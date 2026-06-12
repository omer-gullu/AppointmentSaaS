using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Core.DTOs;

public class StaffListItemDto
{
    public int AppUserID { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string? GoogleCalendarId { get; set; }
    public bool HasGoogleConnected { get; set; }
    public bool Status { get; set; }

    public static StaffListItemDto FromEntity(AppUser u) => new()
    {
        AppUserID = u.AppUserID,
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        PhoneNumber = u.PhoneNumber,
        Specialization = u.Specialization,
        GoogleCalendarId = u.GoogleCalendarId,
        HasGoogleConnected = !string.IsNullOrEmpty(u.GoogleRefreshToken),
        Status = u.Status
    };
}
