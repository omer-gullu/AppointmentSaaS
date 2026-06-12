namespace Appointment_SaaS.Core.Utilities;

/// <summary>
/// Randevu müşteri telefonu: WhatsApp JID, +90/90/0 varyantları ve yalnızca rakamlar için ortak normalizasyon.
/// </summary>
public static class AppointmentPhoneNormalizer
{
    /// <summary>
    /// WhatsApp JID ("905...@s.whatsapp.net"), +90, 90, 0 önekleri sonrası 10 haneli yerel çekirdek (5XXXXXXXXX).
    /// </summary>
    public static string NormalizeCore(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = raw.Trim();
        var at = s.IndexOf('@', StringComparison.Ordinal);
        if (at > 0) s = s[..at];
        s = new string(s.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.StartsWith("90", StringComparison.Ordinal) && s.Length > 10)
            s = s[2..];
        if (s.StartsWith("0", StringComparison.Ordinal) && s.Length > 10)
            s = s[1..];
        return s;
    }

    /// <summary>
    /// Veritabanındaki <c>CustomerPhone</c> ile eşleştirmek için aday değerler (EF <c>IN</c> sorgusu).
    /// </summary>
    public static IReadOnlyList<string> BuildLookupKeys(string? raw)
    {
        var core = NormalizeCore(raw);
        if (string.IsNullOrEmpty(core)) return Array.Empty<string>();

        var set = new HashSet<string>(StringComparer.Ordinal)
        {
            core,
            "0" + core,
            "90" + core,
            "+90" + core,
            "90" + core + "@s.whatsapp.net"
        };
        return set.ToList();
    }
}
