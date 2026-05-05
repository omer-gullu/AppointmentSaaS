using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Appointment_SaaS.Core.Interfaces;

namespace Appointment_SaaS.Core.Entities
{
    public class AppUser : ITenantEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AppUserID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string? OtpCode { get; set; }
        public DateTime? OtpExpiry { get; set; }
        public DateTime? LastOtpRequestDate { get; set; }
        public int AccessFailedCount { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }
        public string PhoneNumber { get; set; }
        public string? SecurityStamp { get; set; } = Guid.NewGuid().ToString();

        // Ustanın uzmanlık alanı (AI bunu prompta kullanabilir)
        public string? Specialization { get; set; }

        /// <summary>
        /// Personele ait Google Takvim ID'si.
        /// OAuth ile otomatik olarak personelin Gmail adresi olarak set edilir.
        /// Randevu oluşturulurken bu takvime yazılır.
        /// </summary>
        public string? GoogleCalendarId { get; set; }

        /// <summary>
        /// Personele ait Google OAuth refresh token.
        /// Bu token ile her seferinde taze access_token alınır.
        /// OAuth callback'te kaydedilir.
        /// </summary>
        public string? GoogleRefreshToken { get; set; }

        public bool Status { get; set; } = true;
        public DateTime? TrialStartDate { get; set; }
        public DateTime? TrialEndDate { get; set; }

        // İlişki: Her kullanıcı bir işletmeye (Tenant) aittir.
        public int TenantID { get; set; }
        public Tenant Tenant { get; set; }

        // İlişki: Bu ustanın randevuları
        public ICollection<Appointment> Appointments { get; set; }
    }
}