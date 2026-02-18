using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.Entities
{
    public class OperationClaim
    {
        public int Id { get; set; }
        public string Name { get; set; } // Örn: "Admin", "User"
    }
}
