namespace Appointment_SaaS.Core.Utilities;

/// <summary>
/// OTP giriş ayarları (appsettings: OtpLogin).
/// FixedCode yalnızca Development ortamında kullanılır (E2E / yerel test).
/// </summary>
public class OtpLoginOptions
{
    public const string SectionName = "OtpLogin";

    /// <summary>6 haneli sabit kod (ör. 123456). Production'da yok sayılır.</summary>
    public string? FixedCode { get; set; }
}
