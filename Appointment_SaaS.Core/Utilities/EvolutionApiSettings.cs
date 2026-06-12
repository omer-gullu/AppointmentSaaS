namespace Appointment_SaaS.Core.Utilities;

public class EvolutionApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string GlobalApiKey { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string DefaultInstance { get; set; } = "appointment";

    /// <summary>
    /// Bu öneklerle başlayan numaralara (90… formatında) WhatsApp gönderilmez — E2E / test randevuları.
    /// Örnek: "905320000" → 5320000123
    /// </summary>
    public List<string> SandboxPhonePrefixes { get; set; } = new();

    /// <summary>
    /// true ise yalnızca <see cref="AllowedWhatsAppNumbers"/> listesindeki numaralara gönderilir (yerel/test).
    /// </summary>
    public bool EnforceWhatsAppAllowlist { get; set; }

    /// <summary>
    /// İzin verilen alıcılar (0532…, 90532… veya 532… formatında). EnforceWhatsAppAllowlist=true iken zorunlu.
    /// </summary>
    public List<string> AllowedWhatsAppNumbers { get; set; } = new();
}
