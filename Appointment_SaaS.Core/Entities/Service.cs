using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.Entities
{
    public class Service
    {
        public int ServiceID { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int DurationInMinutes { get; set; } // Randevu takvimi için çok kritik

        // İlişki: Her hizmet bir işletmeye (Tenant) aittir.
        public int TenantID { get; set; }
        public Tenant Tenant { get; set; }
    }
}
