using System.ComponentModel.DataAnnotations.Schema;

namespace Appointment_SaaS.Core.Entities
{
    /// <summary>
    /// Bir randevuya bağlı çoklu hizmet satırı (junction).
    /// </summary>
    public class AppointmentServiceLink
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AppointmentServiceLinkID { get; set; }
        public int AppointmentID { get; set; }
        public Appointment Appointment { get; set; } = null!;
        public int ServiceID { get; set; }
        public Service Service { get; set; } = null!;
        /// <summary>0 tabanlı sıra (AI sırası).</summary>
        public int SortOrder { get; set; }
    }
}
