using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.DTOs
{
    public class ServiceCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; } // Randevu ne kadar sürecek?
        public int TenantID { get; set; } // Hangi dükkana ait?
    }
}
