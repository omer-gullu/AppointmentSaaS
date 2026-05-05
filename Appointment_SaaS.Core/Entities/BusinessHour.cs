using System;
using System.ComponentModel.DataAnnotations.Schema;
using Appointment_SaaS.Core.Interfaces;

namespace Appointment_SaaS.Core.Entities
{
    public class BusinessHour : ITenantEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BusinessHourID { get; set; }
        public int DayOfWeek { get; set; } // 0 = Sunday, 1 = Monday vb.
        public TimeSpan OpenTime { get; set; }
        public TimeSpan CloseTime { get; set; }
        public bool IsClosed { get; set; }

        public int TenantID { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public virtual Tenant? Tenant { get; set; }
    }
}
