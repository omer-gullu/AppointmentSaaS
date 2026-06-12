namespace Appointment_SaaS.WebUI.Models;

public class SetupFirstStaffViewModel
{
    public int TenantId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string? ErrorMessage { get; set; }
}
