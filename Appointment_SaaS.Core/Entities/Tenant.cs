using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.Entities
{
    public class Tenant
    {
        public int TenantID { get; set; }
        public string Name { get; set; }
        public string WabaID { get; set; } // WhatsApp Business Account ID
        public string PhoneNumber { get; set; } // İşletme WhatsApp numarası
        public string Address { get; set; }
        public string ApiKey { get; set; } // n8n veya dış dünya ile güvenli iletişim için
        public DateTime CreatedAt { get; set; }
        public int MessageCount { get; set; } = 0;
        public bool IsBotActive { get; set; } = true;
        public bool IsActive { get; set; }

        // İlişki: Her işletme bir sektöre bağlıdır.
        public int SectorID { get; set; }
        public Sector Sector { get; set; }

        public ICollection<Service> Services { get; set; }
        public ICollection<Appointment> Appointments { get; set; }
    }
}
