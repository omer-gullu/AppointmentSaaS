using Appointment_SaaS.Core.DTOs;

namespace Appointment_SaaS.WebUI.Models;

public class BlockedPhonesViewModel
{
    public List<TenantBlockedPhoneDto> Items { get; set; } = new();
    public string NewPhone { get; set; } = string.Empty;
    public string? NewNote { get; set; }
}
