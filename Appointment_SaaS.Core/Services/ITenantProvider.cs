namespace Appointment_SaaS.Core.Services;

/// <summary>
/// Mevcut HTTP isteğinden (JWT Token veya Cookie) TenantId bilgisini çeken servis.
/// API projesi ve WebUI projesi farklı implementasyonlar sağlayabilir.
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// Aktif kullanıcının ait olduğu TenantId değerini döndürür.
    /// Eğer kullanıcı giriş yapmamışsa veya TenantId bulunamazsa null döner.
    /// </summary>
    int? GetTenantId();
}
