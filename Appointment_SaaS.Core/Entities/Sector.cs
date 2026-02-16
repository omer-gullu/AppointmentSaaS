using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.Entities
{

    public class Sector
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SectorID { get; set; }
        public string Name { get; set; } // Sektör adı
        public string? DefaultPrompt { get; set; } // Bu sektöre özel AI ana talimatı
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // İlişki: Bir sektörün birçok işletmesi (Tenant) olabilir.

        [JsonIgnore]
        public virtual ICollection<Tenant>? Tenants { get; set; } = new List<Tenant>();
    }
}
