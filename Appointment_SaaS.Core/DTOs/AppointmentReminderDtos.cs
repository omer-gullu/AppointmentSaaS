namespace Appointment_SaaS.Core.DTOs;

public class AppointmentReminderPendingDto
{
    public int AppointmentId { get; set; }
    public int TenantId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string InstanceName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
}

public class AppointmentReminderRunResultDto
{
    public int Total { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
}
