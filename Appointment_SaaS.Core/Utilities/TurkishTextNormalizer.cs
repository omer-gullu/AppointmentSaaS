using System.Globalization;

namespace Appointment_SaaS.Core.Utilities;

/// <summary>Türkçe ad-soyad normalizasyonu (NVİ doğrulaması için).</summary>
public static class TurkishTextNormalizer
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string ToTurkishTitleCase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return Tr.TextInfo.ToTitleCase(value.Trim().ToLower(Tr));
    }

    /// <summary>Son kelime soyad, öncekiler ad (NVİ servisi formatı).</summary>
    public static (string Ad, string Soyad)? SplitTurkishFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return null;

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        var ad = string.Join(" ", parts.Take(parts.Length - 1)).ToUpper(Tr);
        var soyad = parts[^1].ToUpper(Tr);
        return (ad, soyad);
    }
}
