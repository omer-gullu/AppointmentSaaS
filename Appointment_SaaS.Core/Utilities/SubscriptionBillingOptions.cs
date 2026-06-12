namespace Appointment_SaaS.Core.Utilities;

/// <summary>
/// Ücretli abonelik yenilemesinde İyzico webhook gecikmesi için erişim tamponu.
/// </summary>
public class SubscriptionBillingOptions
{
    public const string SectionName = "SubscriptionBilling";

    /// <summary>
    /// SubscriptionEndDate sonrası hizmetin açık kalacağı saat (yenileme gecikmesi).
    /// DB'ye eklenmez; yalnızca erişim kontrolünde kullanılır.
    /// </summary>
    public int RenewalGraceHours { get; set; } = 12;
}
