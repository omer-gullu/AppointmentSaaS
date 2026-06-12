using System.ComponentModel.DataAnnotations.Schema;
using Appointment_SaaS.Core.Interfaces;

namespace Appointment_SaaS.Core.Entities;

/// <summary>
/// WhatsApp asistanının yanıt vermeyeceği numaralar (gri liste), tenant başına.
/// </summary>
public class TenantBlockedPhone : ITenantEntity
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TenantBlockedPhoneID { get; set; }

    public int TenantID { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Normalize çekirdek: 5XXXXXXXXX (10 hane).</summary>
    public string PhoneCore { get; set; } = string.Empty;

    /// <summary>Manual | SelfOptOut</summary>
    public string Source { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
