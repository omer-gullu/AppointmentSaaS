using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract;

public interface ITenantPlanService
{
    Task<ChangePlanInitResponseDto> InitializePlanChangeAsync(Tenant tenant, AppUser owner, ChangePlanInitRequestDto request, string callbackUrl);
    Task<bool> CompletePlanChangePaymentAsync(Tenant tenant, string checkoutToken);
    Task ApplySubscriptionFromWebhookAsync(Tenant tenant, string? paymentId, string? rawBody, bool isSuccess, bool isFailure, bool isUpgrade);
    /// <summary>İyzico'da aktif abonelik varsa askıda tenant'ı ve bitiş tarihini düzeltir.</summary>
    Task<bool> TryReconcileFromIyzicoAsync(Tenant tenant, CancellationToken cancellationToken = default);

    /// <summary>PendingPlanEffectiveDate gelmiş ücretli plan geçişlerini uygular.</summary>
    Task<int> ApplyDueScheduledPlanChangesAsync(CancellationToken cancellationToken = default);
}
