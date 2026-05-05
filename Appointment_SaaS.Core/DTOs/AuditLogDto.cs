using System;

namespace Appointment_SaaS.Core.DTOs
{
    public class AuditLogDto
    {
        public int AuditLogID { get; set; }
        public int? UserId { get; set; }
        public string? UserFullName { get; set; }
        public int? TenantId { get; set; }
        public string? TenantName { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string? OldValues { get; set; } // JSON
        public string? NewValues { get; set; } // JSON
        public DateTime Timestamp { get; set; }
        public string? IpAddress { get; set; }
        
        // Smart Labeling için Frontend'de işlense de, DTO'da taslak olarak bulunması iyi olabilir.
        // Veya Frontend kendisi parse edecek. JSON göndereceğiz, JS parse edip Smart Labeling uygulayacak.
    }
}
