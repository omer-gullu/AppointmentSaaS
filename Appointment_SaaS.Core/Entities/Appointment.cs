using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.Entities
{
    public class Appointment
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AppointmentID { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string Status { get; set; }
        public string Note { get; set; } // AI'dan gelen özel notlar
        public bool IsConfirmed { get; set; } // Onaylı mı?

        // İlişki: Randevu hangi işletmeye ait?
        public int TenantID { get; set; }
        public Tenant Tenant { get; set; }

        // İlişki: Hangi hizmet için randevu alındı?
        public int ServiceID { get; set; }
        public Service Service { get; set; }

        // Randevu hangi ustaya/personele alındı?
        public int AppUserID { get; set; }
        public AppUser AppUser { get; set; }
    }
}
