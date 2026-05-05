using System.ComponentModel.DataAnnotations.Schema;

namespace Appointment_SaaS.Core.Entities;

/// <summary>
/// Kritik işlemlerin (randevu oluşturma/güncelleme/silme, kullanıcı değişiklikleri vb.)
/// kim tarafından, ne zaman, hangi IP'den yapıldığını kaydeden merkezi denetim tablosu.
/// n8n workflow hataları da bu tabloya Source="n8n" ile yazılır.
/// </summary>
public class AuditLog
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int AuditLogID { get; set; }

    /// <summary>İşlemi yapan kullanıcının ID'si (JWT'den okunur)</summary>
    public int? UserId { get; set; }

    /// <summary>İşlemin yapıldığı tenant (JWT'den okunur)</summary>
    public int? TenantId { get; set; }

    /// <summary>Yapılan işlem türü: Create, Update, Delete, WorkflowError vb.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Etkilenen entity adı: Appointment, Service, AppUser, n8n vb.</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Etkilenen kaydın Primary Key değeri</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Güncelleme/silme öncesi değerler (JSON formatında)</summary>
    public string? OldValues { get; set; }

    /// <summary>Güncelleme/oluşturma sonrası değerler (JSON formatında)</summary>
    public string? NewValues { get; set; }

    /// <summary>İşlemin yapıldığı tarih ve saat (UTC)</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>İşlemin yapıldığı IP adresi</summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Logun kaynağı: "API", "n8n", "System", "Webhook"
    /// Admin panelinde filtreleme için kullanılır.
    /// </summary>
    public string Source { get; set; } = "API";

    /// <summary>
    /// Log seviyesi: "Info", "Warning", "Error"
    /// Admin panelinde renk kodlaması ve filtreleme için kullanılır.
    /// </summary>
    public string LogLevel { get; set; } = "Info";
}
