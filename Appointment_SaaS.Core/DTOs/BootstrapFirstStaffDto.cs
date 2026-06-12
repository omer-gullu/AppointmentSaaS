namespace Appointment_SaaS.Core.DTOs;

/// <summary>
/// Kayıt sonrası ilk personel (işletme sahibi / Manager) — tenant'ta henüz AppUser yokken.
/// </summary>
public class BootstrapFirstStaffDto
{
    public int TenantId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Specialization { get; set; }
}
