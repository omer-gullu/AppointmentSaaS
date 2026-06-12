using Appointment_SaaS.Core.Constants;

namespace Appointment_SaaS.Core.Utilities;

public enum PlanTransitionKind
{
    Allowed,
    SamePlan,
    DowngradeToTrialNotAllowed,
    InvalidTargetPlan,
    InvalidTargetCycle
}

public readonly record struct PlanTransitionResult(
    bool IsAllowed,
    PlanTransitionKind Kind,
    string? Message = null)
{
    public static PlanTransitionResult Ok() =>
        new(true, PlanTransitionKind.Allowed);

    public static PlanTransitionResult Fail(PlanTransitionKind kind, string message) =>
        new(false, kind, message);
}

/// <summary>
/// Plan ve fatura döngüsü geçiş kuralları (Trial → ücretli serbest; ücretli → Trial yasak).
/// </summary>
public static class PlanTransitionValidator
{
    private static readonly HashSet<string> PaidPlans = new(StringComparer.OrdinalIgnoreCase)
    {
        "Starter", "Pro", "Business"
    };

    public static PlanTransitionResult Validate(
        string? currentPlanType,
        string? currentBillingCycle,
        string? targetPlanType,
        string? targetBillingCycle,
        bool isTrial)
    {
        if (string.Equals(targetPlanType, "Trial", StringComparison.OrdinalIgnoreCase)
            || string.Equals(targetPlanType, "trial", StringComparison.OrdinalIgnoreCase))
        {
            if (!isTrial)
            {
                return PlanTransitionResult.Fail(
                    PlanTransitionKind.DowngradeToTrialNotAllowed,
                    "Ücretli plandan deneme planına geçiş yapılamaz.");
            }
            return PlanTransitionResult.Fail(
                PlanTransitionKind.InvalidTargetPlan,
                "Deneme planına geçiş için mevcut deneme sürenizi kullanın.");
        }

        var targetPlan = NormalizePlan(targetPlanType);
        var targetCycle = BillingCycles.Normalize(targetBillingCycle);

        if (!PaidPlans.Contains(targetPlan))
        {
            return PlanTransitionResult.Fail(
                PlanTransitionKind.InvalidTargetPlan,
                "Hedef plan geçersiz. Starter, Pro veya Business seçin.");
        }

        if (!BillingCycles.IsValid(targetBillingCycle))
        {
            return PlanTransitionResult.Fail(
                PlanTransitionKind.InvalidTargetCycle,
                "Fatura döngüsü Monthly veya Yearly olmalıdır.");
        }

        var currentPlan = isTrial ? "Trial" : NormalizePlan(currentPlanType);
        var currentCycle = BillingCycles.Normalize(currentBillingCycle);

        if (!isTrial && string.Equals(currentPlan, "Trial", StringComparison.OrdinalIgnoreCase) == false)
        {
            if (string.Equals(targetPlan, "Trial", StringComparison.OrdinalIgnoreCase))
            {
                return PlanTransitionResult.Fail(
                    PlanTransitionKind.DowngradeToTrialNotAllowed,
                    "Ücretli plandan deneme planına geçiş yapılamaz.");
            }
        }

        if (!isTrial
            && PaidPlans.Contains(currentPlan)
            && string.Equals(currentPlan, targetPlan, StringComparison.OrdinalIgnoreCase)
            && string.Equals(currentCycle, targetCycle, StringComparison.OrdinalIgnoreCase))
        {
            return PlanTransitionResult.Fail(
                PlanTransitionKind.SamePlan,
                "Zaten bu plan ve fatura döngüsündesiniz.");
        }

        return PlanTransitionResult.Ok();
    }

    public static bool RequiresCheckoutForCycleChange(string? currentBillingCycle, string? targetBillingCycle) =>
        !string.Equals(
            BillingCycles.Normalize(currentBillingCycle),
            BillingCycles.Normalize(targetBillingCycle),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizePlan(string? planType)
    {
        if (string.IsNullOrWhiteSpace(planType))
            return "Trial";

        var p = planType.Trim();
        if (string.Equals(p, "trial", StringComparison.OrdinalIgnoreCase))
            return "Trial";

        foreach (var paid in PaidPlans)
        {
            if (string.Equals(p, paid, StringComparison.OrdinalIgnoreCase))
                return paid;
        }

        return p;
    }
}
