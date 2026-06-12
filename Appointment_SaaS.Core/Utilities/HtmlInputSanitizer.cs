using System.Net;
using System.Text.RegularExpressions;

namespace Appointment_SaaS.Core.Utilities;

/// <summary>CSRF dışı XSS/HTML enjeksiyonuna karşı metin temizleme ve encode.</summary>
public static class HtmlInputSanitizer
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex JsEventRegex = new(@"\bon\w+\s*=", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SqlInjectionRegex = new(
        @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|UNION|ALTER|CREATE|EXEC|EXECUTE|xp_|sp_)\b|--|;|'|""|\/\*|\*\/)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SafeNameRegex = new(@"^[a-zA-ZçÇğĞıİöÖşŞüÜ\s\-\.]+$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^(\+?90|0)?[5][0-9]{9}$", RegexOptions.Compiled);

    public static string SanitizeText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sanitized = HtmlTagRegex.Replace(input, "");
        sanitized = JsEventRegex.Replace(sanitized, "");
        sanitized = Regex.Replace(sanitized, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
        return sanitized.Trim();
    }

    public static string SanitizeName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sanitized = SanitizeText(input);
        sanitized = Regex.Replace(sanitized, @"[^a-zA-ZçÇğĞıİöÖşŞüÜ\s\-\.]", "");
        return sanitized.Trim();
    }

    public static string SanitizePhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        return Regex.Replace(input, @"[^\d+]", "");
    }

    public static string HtmlEncode(string? input) =>
        string.IsNullOrEmpty(input) ? string.Empty : WebUtility.HtmlEncode(input);

    /// <summary>JavaScript string literal içine güvenli yerleştirme.</summary>
    public static string EscapeForJavaScript(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("<", "\\u003c")
            .Replace(">", "\\u003e");
    }

    public static bool ContainsSqlInjection(string? input) =>
        !string.IsNullOrWhiteSpace(input) && SqlInjectionRegex.IsMatch(input);

    public static bool ContainsXss(string? input) =>
        !string.IsNullOrWhiteSpace(input) && (HtmlTagRegex.IsMatch(input) || JsEventRegex.IsMatch(input));

    public static bool IsValidName(string? input) =>
        !string.IsNullOrWhiteSpace(input) && SafeNameRegex.IsMatch(input.Trim());

    public static bool IsValidTurkishPhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        var cleaned = SanitizePhone(input);
        return PhoneRegex.IsMatch(cleaned);
    }
}
