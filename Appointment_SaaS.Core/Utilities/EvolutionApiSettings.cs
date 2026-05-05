namespace Appointment_SaaS.Core.Utilities;

public class EvolutionApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string GlobalApiKey { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string DefaultInstance { get; set; } = "appointment";
}
