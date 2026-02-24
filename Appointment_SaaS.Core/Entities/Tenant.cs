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
        public string? WabaID { get; set; } // WhatsApp Business Account ID
        public string PhoneNumber { get; set; } // İşletme WhatsApp numarası
        public string Address { get; set; }
        public string ApiKey { get; set; } // n8n veya dış dünya ile güvenli iletişim için
        public DateTime CreatedAt { get; set; }
        public int MessageCount { get; set; } = 0;
        public bool IsBotActive { get; set; } = true;
        public bool IsActive { get; set; }
        public bool IsTrial { get; set; }
        public DateTime SubscriptionEndDate { get; set; } // Deneme veya abonelik bitişi
        public bool IsSubscriptionActive { get; set; } // Ödeme iptal edildi mi?
        public string StripeCustomerId { get; set; } // Ödeme altyapısı (Stripe/Iyzico) için ID
        public bool AutoRenew { get; set; } = true; // Otomatik yenileme açık mı?

        // İlişki: Her işletme bir sektöre bağlıdır.
        public int SectorID { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public virtual Sector? Sector { get; set; }

        public virtual ICollection<Service>? Services { get; set; } = new List<Service>();
        public virtual ICollection<Appointment>? Appointments { get; set; } = new List<Appointment>();
    }
}
