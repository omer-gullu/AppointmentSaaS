namespace Appointment_SaaS.Core.Interfaces;

/// <summary>
/// TenantID içeren tüm entity'lerin uygulaması gereken interface.
/// AppDbContext, bu interface'i uygulayan entity'lere otomatik Global Query Filter ekler.
/// Böylece her sorgu sadece ilgili tenant'ın verilerini döndürür.
/// </summary>
public interface ITenantEntity
{
    int TenantID { get; set; }
}
