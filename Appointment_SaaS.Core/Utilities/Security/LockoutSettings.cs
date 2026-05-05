namespace Appointment_SaaS.Core.Utilities.Security;

/// <summary>
/// Brute-force koruması için yapılandırılabilir kilitleme ayarları.
/// Değerler appsettings.json > LockoutSettings bölümünden okunur.
/// </summary>
public class LockoutSettings
{
    /// <summary>Kilitleme tetiklenmeden önce izin verilen maksimum başarısız deneme sayısı.</summary>
    public int MaxFailedAccessAttempts { get; set; } = 3;

    /// <summary>Kilitleme süresi (dakika cinsinden).</summary>
    public int DefaultLockoutTimeSpanInMinutes { get; set; } = 10;
}
