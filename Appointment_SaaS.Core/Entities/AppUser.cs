using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.Entities
{
    public class AppUser
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AppUserID { get; set; } // Senin stilin
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string? PasswordHash { get; set; }
        public string PhoneNumber { get; set; }

        // Ustanın uzmanlık alanı (AI bunu prompta kullanabilir)
        public string Specialization { get; set; }

        // İlişki: Her kullanıcı bir işletmeye (Tenant) aittir.
        public int TenantID { get; set; }
        public Tenant Tenant { get; set; }

        // İlişki: Bu ustanın randevuları
        public ICollection<Appointment> Appointments { get; set; }
    }
}
