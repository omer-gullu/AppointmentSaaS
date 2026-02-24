using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.DTOs
{
    public class TenantCreateDto
    {
        public string Name { get; set; } 
        public int SectorID { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }

    }
}
