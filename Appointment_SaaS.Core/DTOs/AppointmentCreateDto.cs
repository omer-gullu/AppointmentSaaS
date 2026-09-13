using System.Text.Json.Serialization;
using Appointment_SaaS.Core.Utilities;

namespace Appointment_SaaS.Core.DTOs
{

    public class AppointmentCreateDto
    {
        public string BusinessPhone { get; set; } = string.Empty; // N8n'den işletme numarası gelir
        public int TenantID { get; set; } // Controller tarafından set edilecek
        public int ServiceID { get; set; }
        /// <summary>
        /// Aynı randevuda birden fazla hizmet (sıra korunur). Boş veya null ise yalnızca ServiceID kullanılır.
        /// n8n [20,18] veya "20,18" gönderebilir.
        /// </summary>
        [JsonConverter(typeof(FlexibleIntListJsonConverter))]
        public List<int>? ServiceIds { get; set; }
        public int? AppUserID { get; set; } // (Usta seçildiyse)
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        
        public DateTime StartDate { get; set; } // AI'dan gelen YYYY-MM-DDTHH:mm:ss formatında
        public DateTime EndDate { get; set; }
        public string? Note { get; set; }
        public string? GoogleEventID { get; set; } // Google Takvim senkronizasyonu için
    }

}
