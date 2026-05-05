using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Core.DTOs;

/// <summary>
/// n8n'in AI Agent prompt'una ekleyeceği müşteri geçmişi verisi.
/// GET /api/Appointments/customer/{phone}?tenantId=X endpoint'inden döner.
/// </summary>
public class CustomerHistoryDto
{
    /// <summary>Müşteri bu işletmeye daha önce geldi mi?</summary>
    public bool IsReturningCustomer { get; set; }

    /// <summary>Toplam ziyaret sayısı</summary>
    public int TotalVisits { get; set; }

    /// <summary>Müşterinin adı (son randevudan)</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Son randevu tarihi</summary>
    public DateTime? LastVisitDate { get; set; }

    /// <summary>Son alınan hizmet adı</summary>
    public string? LastServiceName { get; set; }

    /// <summary>Son randevunun durumu</summary>
    public string? LastVisitStatus { get; set; }

    /// <summary>
    /// AI prompt'una eklenecek hazır özet metin.
    /// Örn: "Düzenli müşteri. Son ziyaret: 15 Nisan 2026 - Saç Kesimi (3. ziyaret)"
    /// </summary>
    public string SummaryForAI { get; set; } = string.Empty;
}

