namespace Appointment_SaaS.Core.Utilities;

public class IyzicoSettings
{
    public bool Enabled { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://sandbox-api.iyzipay.com";

    public string TrialPlanCode { get; set; } = string.Empty;
    public string StarterMonthlyPlanCode { get; set; } = string.Empty;
    public string StarterYearlyPlanCode { get; set; } = string.Empty;
    public string BusinessMonthlyPlanCode { get; set; } = string.Empty;
    public string BusinessYearlyPlanCode { get; set; } = string.Empty;
    public string ProMonthlyPlanCode { get; set; } = string.Empty;
    public string ProYearlyPlanCode { get; set; } = string.Empty;

    /// <summary>
    /// Iyzico webhook endpoint'ini (API) doğrulamak için opsiyonel shared secret.
    /// </summary>
    public string? WebhookSecret { get; set; }
}

