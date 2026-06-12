using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract;

public record IyzicoSubscriptionInitResult(
    string? IyzicoUserKey,
    string? IyzicoCardToken,
    string? SubscriptionReferenceCode
);

public record IyzicoCheckoutFormInitResult(
    string CheckoutFormContent,
    string Token
);

public record IyzicoCheckoutVerifyResult(
    string CheckoutToken,
    string? SubscriptionReferenceCode,
    string? CustomerReferenceCode
);

public record IyzicoSubscriptionDetailResult(
    string SubscriptionReferenceCode,
    string? SubscriptionStatus,
    DateTime? EndDate,
    string? PricingPlanReferenceCode
);

public interface IIyzicoPaymentService
{
    /// <summary>
    /// Abonelik için İyzico Checkout (3D Secure) formunu başlatır.
    /// Geriye ekranda gösterilecek script kodunu (CheckoutFormContent) döner.
    /// </summary>
    Task<IyzicoCheckoutFormInitResult> InitializeSubscriptionCheckoutFormAsync(
        Tenant tenant,
        string customerEmail,
        string customerFullName,
        string customerPhone,
        string customerIdentityNumber,
        string planType,
        string billingCycle,
        string callbackUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checkout token doğrular; mümkünse gerçek abonelik referans kodunu çözümler.
    /// </summary>
    Task<IyzicoCheckoutVerifyResult> VerifyCheckoutFormAsync(
        string token,
        string? pricingPlanReferenceCode = null,
        string? excludeSubscriptionReferenceCode = null,
        CancellationToken cancellationToken = default);

    Task CancelSubscriptionAsync(string subscriptionReferenceCode, CancellationToken cancellationToken = default);

    Task UpgradeSubscriptionAsync(
        string subscriptionReferenceCode,
        string newPricingPlanReferenceCode,
        CancellationToken cancellationToken = default);

    Task<IyzicoSubscriptionDetailResult?> GetSubscriptionDetailAsync(
        string subscriptionReferenceCode,
        CancellationToken cancellationToken = default);

    Task<IyzicoSubscriptionDetailResult?> ResolveLatestActiveSubscriptionAsync(
        string pricingPlanReferenceCode,
        string? excludeSubscriptionReferenceCode = null,
        string? preferSubscriptionReferenceCode = null,
        CancellationToken cancellationToken = default);

    Task<IyzicoSubscriptionDetailResult?> ResolveLatestActiveByCustomerAsync(
        string customerReferenceCode,
        string? preferSubscriptionReferenceCode = null,
        CancellationToken cancellationToken = default);

    string GetPricingPlanReferenceCode(string planType, string billingCycle);

    /// <summary>
    /// Deneme (trial) kaydı: tek seferlik checkout form — küçük tutar tahsil, callback sonrası iade için kullanılır.
    /// </summary>
    Task<IyzicoCheckoutFormInitResult> InitializeTrialCardValidationCheckoutAsync(
        Tenant tenant,
        string customerEmail,
        string customerFullName,
        string customerPhone,
        string customerIdentityNumber,
        string callbackUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trial checkout token doğrulama + ilk kalem üzerinden tam iade (kart doğrulama).
    /// </summary>
    Task VerifyTrialCheckoutFormAndRefundAsync(string token, CancellationToken cancellationToken = default);
}

