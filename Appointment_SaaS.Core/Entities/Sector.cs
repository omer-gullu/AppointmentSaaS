using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.Entities
{
    public class Sector
    {
        public int SectorID { get; set; }
        public string Name { get; set; } // Sektör adı
        public string DefaultPrompt { get; set; } // Bu sektöre özel AI ana talimatı

        // İlişki: Bir sektörün birçok işletmesi (Tenant) olabilir.
        public ICollection<Tenant> Tenants { get; set; }
    }
}
