using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.Utilities;

public class ErrorDetails
{
    public int StatusCode { get; set; }
    public string Message { get; set; }
    // Hata modelini JSON formatına çevirmek için
    public override string ToString() => System.Text.Json.JsonSerializer.Serialize(this);
}
