using Appointment_SaaS.Core.Entities;
using System.Linq;

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
    public List<int> ServiceIds { get; set; } = new();
    public string ServiceName { get; set; } = string.Empty;
    public int AppUserID { get; set; }
    public string? GoogleEventID { get; set; }
    public bool IsConfirmed { get; set; }

    public static AppointmentListItemDto FromEntity(Appointment a)
    {
        var links = (a.AppointmentServiceLinks ?? Enumerable.Empty<AppointmentServiceLink>())
            .OrderBy(l => l.SortOrder)
            .ToList();
        var ids = links.Select(l => l.ServiceID).Where(id => id > 0).ToList();
        if (ids.Count == 0 && a.ServiceID > 0)
            ids.Add(a.ServiceID);

        var names = links
            .Select(l => l.Service?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .ToList();
        if (names.Count == 0 && !string.IsNullOrWhiteSpace(a.Service?.Name))
            names.Add(a.Service!.Name);

        return new AppointmentListItemDto
        {
            AppointmentID = a.AppointmentID,
            CustomerName = a.CustomerName,
            CustomerPhone = a.CustomerPhone,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            Status = a.Status,
            ServiceID = a.ServiceID,
            ServiceIds = ids,
            ServiceName = string.Join(", ", names),
            AppUserID = a.AppUserID,
            GoogleEventID = a.GoogleEventID,
            IsConfirmed = a.IsConfirmed
        };
    }
}
