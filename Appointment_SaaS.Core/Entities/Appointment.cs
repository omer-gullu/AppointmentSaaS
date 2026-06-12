using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Appointment_SaaS.Core.Interfaces;
using Appointment_SaaS.Core.Utilities;

namespace Appointment_SaaS.Core.Entities
{
    public class Appointment : ITenantEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AppointmentID { get; set; }
        public string CustomerName { get; set; }

        /// <summary>
        /// Müşteri telefonu (+90 / 05xx / rakam çekirdeği veya WhatsApp JID). Okuma/API eşlemesi <see cref="AppointmentPhoneNormalizer"/> ile yapılır; eski JID kayıtları için isteğe bağlı SQL: <c>Appointment_SaaS.Data/Scripts/NormalizeAppointmentCustomerPhones.sql</c>.
        /// </summary>
        public string CustomerPhone { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string Status { get; set; }
        public string Note { get; set; } // AI'dan gelen özel notlar
        public bool IsConfirmed { get; set; } // Onaylı mı?

        /// <summary>Yarın hatırlatma mesajı gönderildiyse UTC zaman.</summary>
        public DateTime? ReminderSentAt { get; set; }

        // İlişki: Randevu hangi işletmeye ait?
        public int TenantID { get; set; }
        public Tenant Tenant { get; set; }

        // İlişki: Birincil hizmet (raporlama / geriye dönük uyumluluk). Çoklu hizmet için AppointmentServiceLinks kullanılır.
        public int ServiceID { get; set; }
        public Service Service { get; set; }

        public ICollection<AppointmentServiceLink> AppointmentServiceLinks { get; set; } = new List<AppointmentServiceLink>();

        // Randevu hangi ustaya/personele alındı?
        public int AppUserID { get; set; }
        public AppUser AppUser { get; set; }

        // Google Takvim entegrasyonu için event ID
        public string? GoogleEventID { get; set; }
    }
}
