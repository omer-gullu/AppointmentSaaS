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

    /// <summary>
    /// Deneme kaydında kart doğrulama için tek seferlik tahsilat (TRY). Başarıdan sonra iade edilir.
    /// </summary>
    public decimal TrialCardValidationAmountTry { get; set; } = 1.00m;

    /// <summary>
    /// Refund API için IP (sunucu tarafı çağrı; sandbox için 127.0.0.1 yeterli olabilir).
    /// </summary>
    public string RefundCallerIp { get; set; } = "127.0.0.1";
}

