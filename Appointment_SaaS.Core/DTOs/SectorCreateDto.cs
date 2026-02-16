using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.DTOs
{
    public class SectorCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? DefaultPrompt { get; set; }
    }
}
