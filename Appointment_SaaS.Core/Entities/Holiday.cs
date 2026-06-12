using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.Entities
{
    public class Holiday
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public DateOnly Date { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; } // true = sistem tarafından eklendi

        public Tenant Tenant { get; set; } = null!;
    }
}
