using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.Entities
{
    public class Tenant
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TenantID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } // İşletme WhatsApp numarası
        public string? InstanceName { get; set; } // Evolution API instance adı
        public string Address { get; set; }
        public string ApiKey { get; set; } // n8n veya dış dünya ile güvenli iletişim için
        public DateTime CreatedAt { get; set; }
        public int MessageCount { get; set; } = 0;
        public bool IsBotActive { get; set; } = true;
        public bool IsActive { get; set; }
        public bool IsTrial { get; set; }
        public DateTime SubscriptionEndDate { get; set; }
        public bool IsSubscriptionActive { get; set; } = true;
        public string? StripeCustomerId { get; set; }
        public string? IyzicoUserKey { get; set; }
        public string? IyzicoCardToken { get; set; }
        public string? SubscriptionReferenceCode { get; set; }
        public string? GoogleEmail { get; set; }
        public string? GoogleAccessToken { get; set; }
        public bool AutoRenew { get; set; } = true;

        /// <summary>
        /// Abonelik planı: "Trial", "Starter", "Pro", "Business"
        /// AuthManager.RegisterBusinessOwnerAsync tarafından set edilir.
        /// </summary>
        public string PlanType { get; set; } = "Trial";

        // --- Anti-Fraud & Güvenlik Alanları ---
        public string TrialFingerprint { get; set; } = string.Empty;
        public bool TrialUsed { get; set; } = false;
        public bool IsBlacklisted { get; set; } = false;

        public int SectorID { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public virtual Sector? Sector { get; set; }

        public virtual ICollection<Service>? Services { get; set; } = new List<Service>();
        public virtual ICollection<Appointment>? Appointments { get; set; } = new List<Appointment>();
        public virtual ICollection<BusinessHour>? BusinessHours { get; set; } = new List<BusinessHour>();
        public virtual ICollection<AppUser>? AppUsers { get; set; } = new List<AppUser>();
    }
}