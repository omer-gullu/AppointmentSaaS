using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Core.DTOs;

public class AppointmentListItemDto
{
    public int AppointmentID { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ServiceID { get; set; }
    public int AppUserID { get; set; }
    public string? GoogleEventID { get; set; }
    public bool IsConfirmed { get; set; }

    public static AppointmentListItemDto FromEntity(Appointment a) => new()
    {
        AppointmentID = a.AppointmentID,
        CustomerName = a.CustomerName,
        CustomerPhone = a.CustomerPhone,
        StartDate = a.StartDate,
        EndDate = a.EndDate,
        Status = a.Status,
        ServiceID = a.ServiceID,
        AppUserID = a.AppUserID,
        GoogleEventID = a.GoogleEventID,
        IsConfirmed = a.IsConfirmed
    };
}
