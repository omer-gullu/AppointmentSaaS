using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.DTOs
{

    public class AppointmentCreateDto
    {
        public int TenantID { get; set; }
        public int ServiceID { get; set; }
        public int AppUserID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public string? Note { get; set; }
    }

}
