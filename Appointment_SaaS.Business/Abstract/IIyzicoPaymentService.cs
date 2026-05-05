using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract;

public record IyzicoSubscriptionInitResult(
    string? IyzicoUserKey,
    string? IyzicoCardToken,
    string? SubscriptionReferenceCode
);

public record IyzicoCardInput(
    string CardHolderName,
    string CardNumber,
    string ExpireMonth,
    string ExpireYear,
    string Cvc
);

public interface IIyzicoPaymentService
{
    Task<IyzicoSubscriptionInitResult> InitializeSubscriptionAsync(
        Tenant tenant,
        string customerEmail,
        string customerFullName,
        string customerPhone,
        string customerIdentityNumber,
        IyzicoCardInput card,
        string planType,
        string billingCycle,
        CancellationToken cancellationToken = default);

    Task CancelSubscriptionAsync(string subscriptionReferenceCode, CancellationToken cancellationToken = default);
}

