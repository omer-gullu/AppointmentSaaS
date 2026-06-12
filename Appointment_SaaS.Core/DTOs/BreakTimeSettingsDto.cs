namespace Appointment_SaaS.Core.DTOs;

public class BreakTimeSettingsDto
{
    public bool IsEnabled { get; set; } = true;
    public string StartTime { get; set; } = "12:00";
    public string EndTime { get; set; } = "13:00";
}
