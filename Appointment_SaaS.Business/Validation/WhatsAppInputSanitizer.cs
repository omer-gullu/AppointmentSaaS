using Appointment_SaaS.Core.Utilities;

namespace Appointment_SaaS.Business.Validation;

/// <summary>WhatsApp/n8n girdileri — <see cref="HtmlInputSanitizer"/> üzerinden temizlenir.</summary>
public static class WhatsAppInputSanitizer
{
    public static string SanitizeText(string? input) => HtmlInputSanitizer.SanitizeText(input);
    public static string SanitizeName(string? input) => HtmlInputSanitizer.SanitizeName(input);
    public static string SanitizePhone(string? input) => HtmlInputSanitizer.SanitizePhone(input);
    public static bool ContainsSqlInjection(string? input) => HtmlInputSanitizer.ContainsSqlInjection(input);
    public static bool ContainsXss(string? input) => HtmlInputSanitizer.ContainsXss(input);
    public static bool IsValidName(string? input) => HtmlInputSanitizer.IsValidName(input);
    public static bool IsValidTurkishPhone(string? input) => HtmlInputSanitizer.IsValidTurkishPhone(input);
}
