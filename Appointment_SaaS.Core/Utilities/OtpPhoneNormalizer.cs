using System.Linq;

namespace Appointment_SaaS.Core.Utilities;

/// <summary>OTP giriş/generate için tutarlı telefon formatı (05xxxxxxxxx).</summary>
public static class OtpPhoneNormalizer
{
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90") && digits.Length > 10)
            digits = digits[2..];
        if (digits.StartsWith("0") && digits.Length > 10)
            digits = digits[1..];
        if (digits.Length > 10)
            digits = digits[^10..];

        return digits.Length == 10 ? $"0{digits}" : phone.Trim();
    }
}
