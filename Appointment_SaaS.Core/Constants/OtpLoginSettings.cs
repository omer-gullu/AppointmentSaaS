namespace Appointment_SaaS.Core.Constants;

/// <summary>
/// WhatsApp OTP giriş süreleri. Süre, mesaj başarıyla gönderildikten sonra başlar.
/// </summary>
public static class OtpLoginSettings
{
    /** WhatsApp gecikmesi + kod girişi için (panel/E2E). */
    public const int ValiditySeconds = 90;

    public const int ResendCooldownSeconds = 45;

    public static string ValidityDisplayText => $"{ValiditySeconds} saniye";
}
