using System.Text.RegularExpressions;

namespace Appointment_SaaS.Business.Validation;

/// <summary>
/// WhatsApp ve n8n üzerinden gelen kullanıcı girdilerini XSS, SQL Injection ve 
/// diğer injection saldırılarına karşı temizleyen merkezi sanitization sınıfı.
/// 
/// Kullanım alanları:
/// - Müşteri adı (CustomerName)
/// - Randevu notu (Note)
/// - İşletme adı (BusinessName)
/// - Telefon numarası formatı
/// 
/// FluentValidation kuralları içinde veya doğrudan service katmanında çağrılabilir.
/// </summary>
public static class WhatsAppInputSanitizer
{
    // Tehlikeli HTML/Script tag pattern'leri
    private static readonly Regex HtmlTagRegex = new(@"<[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // SQL Injection riski taşıyan keyword'ler
    private static readonly Regex SqlInjectionRegex = new(
        @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|UNION|ALTER|CREATE|EXEC|EXECUTE|xp_|sp_)\b|--|;|'|""|\/\*|\*\/)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // JavaScript event handler'ları (onclick, onerror vb.)
    private static readonly Regex JsEventRegex = new(
        @"\bon\w+\s*=",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Sadece harf, rakam, boşluk ve temel Türkçe karakterlere izin veren pattern
    private static readonly Regex SafeNameRegex = new(
        @"^[a-zA-ZçÇğĞıİöÖşŞüÜ\s\-\.]+$",
        RegexOptions.Compiled);

    // Türk telefon numarası formatı
    private static readonly Regex PhoneRegex = new(
        @"^(\+?90|0)?[5][0-9]{9}$",
        RegexOptions.Compiled);

    /// <summary>
    /// Genel metin temizleme: HTML tag'lerini, script injection ve SQL injection kalıplarını kaldırır.
    /// </summary>
    public static string SanitizeText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // 1. HTML tag'lerini kaldır
        var sanitized = HtmlTagRegex.Replace(input, "");

        // 2. JavaScript event handler'larını kaldır
        sanitized = JsEventRegex.Replace(sanitized, "");

        // 3. Tehlikeli karakterleri encode et
        sanitized = sanitized
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

        return sanitized.Trim();
    }

    /// <summary>
    /// İsim alanı temizleme: Sadece harfler, boşluk ve tire kabul eder.
    /// </summary>
    public static string SanitizeName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Önce genel temizlik
        var sanitized = SanitizeText(input);

        // Güvenli olmayan karakterleri kaldır (harf, boşluk, tire, nokta hariç)
        sanitized = Regex.Replace(sanitized, @"[^a-zA-ZçÇğĞıİöÖşŞüÜ\s\-\.]", "");

        return sanitized.Trim();
    }

    /// <summary>
    /// Telefon numarası formatını doğrular ve standartlaştırır.
    /// </summary>
    public static string SanitizePhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Sadece rakamları ve + işaretini bırak
        var cleaned = Regex.Replace(input, @"[^\d+]", "");

        return cleaned;
    }

    /// <summary>
    /// Girdi SQL injection riski taşıyor mu kontrol eder.
    /// </summary>
    public static bool ContainsSqlInjection(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return SqlInjectionRegex.IsMatch(input);
    }

    /// <summary>
    /// Girdi XSS riski taşıyor mu kontrol eder.
    /// </summary>
    public static bool ContainsXss(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return HtmlTagRegex.IsMatch(input) || JsEventRegex.IsMatch(input);
    }

    /// <summary>
    /// İsim formatı geçerli mi kontrol eder (sadece harf ve boşluk).
    /// </summary>
    public static bool IsValidName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return SafeNameRegex.IsMatch(input.Trim());
    }

    /// <summary>
    /// Türk telefon numarası formatına uygun mu kontrol eder.
    /// </summary>
    public static bool IsValidTurkishPhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var cleaned = Regex.Replace(input, @"[^\d+]", "");
        return PhoneRegex.IsMatch(cleaned);
    }
}
