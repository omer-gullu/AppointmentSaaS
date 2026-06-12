using Appointment_SaaS.Core.Constants;

namespace Appointment_SaaS.WebUI.Models;

public class ChangePlanViewModel
{
    public int TenantId { get; set; }
    public string CurrentPlanType { get; set; } = "Trial";
    public string CurrentBillingCycle { get; set; } = BillingCycles.Monthly;
    public bool IsTrial { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public bool IsSubscriptionActive { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }
    public int CurrentStaffCount { get; set; }

    /// <summary>Plan seçimi için önce Iyzico yenilemesinin iptal edilmesi gerekir (deneme hariç).</summary>
    public bool RequiresCancelBeforePlanChange =>
        !IsTrial && IsSubscriptionActive && !CancelAtPeriodEnd;

    public bool ShowCancelButton => RequiresCancelBeforePlanChange;

    public bool CanSelectPlan => !RequiresCancelBeforePlanChange;

    public List<ChangePlanCardViewModel> Plans { get; set; } = new()
    {
        new("Starter", BillingCycles.Monthly, PlanPricing.StarterMonthly),
        new("Starter", BillingCycles.Yearly, PlanPricing.StarterYearly),
        new("Pro", BillingCycles.Monthly, PlanPricing.ProMonthly),
        new("Pro", BillingCycles.Yearly, PlanPricing.ProYearly),
    };

    public string? CheckoutFormContent { get; set; }
    public string? PendingPlanDisplayLabel { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string? CancelSuccessMessage { get; set; }
}

public record ChangePlanCardViewModel(string PlanType, string BillingCycle, decimal Price)
{
    public string CycleLabel => BillingCycle == BillingCycles.Yearly ? "Yıllık" : "Aylık";
    public string DisplayName => $"{PlanType} / {CycleLabel}";
    public int StaffLimit => PlanPricing.GetStaffLimit(PlanType);
}
