namespace Appointment_SaaS.Core.DTOs;

public class TenantBlockedPhoneCreateDto
{
    public string Phone { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class TenantBlockedPhoneDto
{
    public int Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WhatsAppOptOutDto
{
    public string Phone { get; set; } = string.Empty;
    public int TenantId { get; set; }
    public string? InstanceName { get; set; }
    /// <summary>WhatsApp pushName veya müşteri adı soyadı (Note alanına yazılır).</summary>
    public string? CustomerName { get; set; }
}

public class WhatsAppBlockedCheckDto
{
    public bool Blocked { get; set; }
    public string PhoneCore { get; set; } = string.Empty;
}
