using Appointment_SaaS.Core.Constants;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;

namespace Appointment_SaaS.Core.DTOs;

/// <summary>API yanıtı — gizli anahtarlar ve ödeme token'ları içermez.</summary>
public class TenantResponseDto
{
    public int TenantID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? InstanceName { get; set; }
    public string Address { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int MessageCount { get; set; }
    public bool IsBotActive { get; set; }
    public bool IsActive { get; set; }
    public bool IsTrial { get; set; }
    public DateTime SubscriptionEndDate { get; set; }
    public bool IsSubscriptionActive { get; set; }
    public string PlanType { get; set; } = "Trial";
    public string BillingCycle { get; set; } = "Monthly";
    public int DaysRemaining { get; set; }
    public string DaysRemainingLabel { get; set; } = string.Empty;
    public int? ScheduledNewPlanDays { get; set; }
    public int TotalAccessDays { get; set; }
    public bool HasScheduledPlanActivation { get; set; }
    public DateTime? PendingPlanEffectiveDate { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public string SubscriptionStatusLabel { get; set; } = string.Empty;
    public bool HasPendingPlanChange { get; set; }
    public bool HasPendingCheckout { get; set; }
    public string? PendingPlanType { get; set; }
    public string? PendingBillingCycle { get; set; }
    public string? PendingPlanDisplayLabel { get; set; }
    public string? GoogleEmail { get; set; }
    public bool HasGoogleConnected { get; set; }
    public bool AutoRenew { get; set; }
    public int SectorID { get; set; }
    public List<BusinessHourDto>? BusinessHours { get; set; }
    public bool BreakTimeEnabled { get; set; }
    public string BreakStartTime { get; set; } = "12:00";
    public string BreakEndTime { get; set; } = "13:00";

    public static TenantResponseDto FromEntity(Tenant tenant)
    {
        var daysDisplay = SubscriptionDisplayHelper.Build(tenant);
        return new TenantResponseDto
        {
            TenantID = tenant.TenantID,
            Name = tenant.Name,
            PhoneNumber = tenant.PhoneNumber,
            InstanceName = tenant.InstanceName,
            Address = tenant.Address,
            CreatedAt = tenant.CreatedAt,
            MessageCount = tenant.MessageCount,
            IsBotActive = tenant.IsBotActive,
            IsActive = tenant.IsActive,
            IsTrial = tenant.IsTrial,
            SubscriptionEndDate = tenant.SubscriptionEndDate,
            IsSubscriptionActive = tenant.IsSubscriptionActive,
            PlanType = tenant.PlanType,
            BillingCycle = tenant.BillingCycle,
            DaysRemaining = daysDisplay.CurrentPeriodDaysRemaining,
            DaysRemainingLabel = daysDisplay.DaysRemainingLabel,
            ScheduledNewPlanDays = daysDisplay.ScheduledNewPlanDays,
            TotalAccessDays = daysDisplay.TotalAccessDays,
            HasScheduledPlanActivation = daysDisplay.HasScheduledPlanActivation,
            PendingPlanEffectiveDate = tenant.PendingPlanEffectiveDate,
            CancelAtPeriodEnd = tenant.CancelAtPeriodEnd,
            SubscriptionStatusLabel = BuildSubscriptionStatusLabel(tenant),
            HasPendingPlanChange = !string.IsNullOrWhiteSpace(tenant.PendingPlanType),
            HasPendingCheckout = !string.IsNullOrWhiteSpace(tenant.PendingCheckoutToken),
            PendingPlanType = tenant.PendingPlanType,
            PendingBillingCycle = tenant.PendingBillingCycle,
            PendingPlanDisplayLabel = BuildPendingPlanDisplayLabel(tenant),
            GoogleEmail = tenant.GoogleEmail,
            HasGoogleConnected = !string.IsNullOrWhiteSpace(tenant.GoogleAccessToken),
            AutoRenew = tenant.AutoRenew,
            SectorID = tenant.SectorID,
            BusinessHours = MapBusinessHours(tenant),
            BreakTimeEnabled = tenant.BreakTimeEnabled,
            BreakStartTime = tenant.BreakStartTime.ToString(@"hh\:mm"),
            BreakEndTime = tenant.BreakEndTime.ToString(@"hh\:mm")
        };
    }

    protected static string BuildSubscriptionStatusLabel(Tenant tenant)
    {
        if (!tenant.IsSubscriptionActive && !tenant.IsActive)
            return "Askıda";
        if (tenant.IsTrial)
            return "Deneme";
        if (!string.IsNullOrWhiteSpace(tenant.PendingCheckoutToken))
            return "Ödeme bekleniyor";
        if (SubscriptionDisplayHelper.Build(tenant).HasScheduledPlanActivation)
            return "Plan değişikliği planlandı";
        if (tenant.CancelAtPeriodEnd)
            return "Dönem sonunda iptal";
        return "Aktif";
    }

    protected static string? BuildPendingPlanDisplayLabel(Tenant tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant.PendingPlanType))
            return null;

        return $"{tenant.PendingPlanType} / {FormatBillingCycleLabel(tenant.PendingBillingCycle)}";
    }

    protected static string FormatBillingCycleLabel(string? cycle) =>
        BillingCycles.Normalize(cycle) == BillingCycles.Yearly ? "Yıllık" : "Aylık";

    protected static List<BusinessHourDto>? MapBusinessHours(Tenant tenant) =>
        tenant.BusinessHours?.Select(b => new BusinessHourDto
        {
            DayOfWeek = b.DayOfWeek,
            OpenTime = b.OpenTime.ToString(@"hh\:mm"),
            CloseTime = b.CloseTime.ToString(@"hh\:mm"),
            IsClosed = b.IsClosed
        }).ToList();
}

/// <summary>Admin listesi — yine de ApiKey / token alanları yok.</summary>
public class TenantAdminResponseDto : TenantResponseDto
{
    public bool IsBlacklisted { get; set; }
    public bool TrialUsed { get; set; }
    public bool HasPendingPaymentToken { get; set; }

    public static new TenantAdminResponseDto FromEntity(Tenant tenant)
    {
        var baseDto = TenantResponseDto.FromEntity(tenant);
        return new TenantAdminResponseDto
        {
            TenantID = baseDto.TenantID,
            Name = baseDto.Name,
            PhoneNumber = baseDto.PhoneNumber,
            InstanceName = baseDto.InstanceName,
            Address = baseDto.Address,
            CreatedAt = baseDto.CreatedAt,
            MessageCount = baseDto.MessageCount,
            IsBotActive = baseDto.IsBotActive,
            IsActive = baseDto.IsActive,
            IsTrial = baseDto.IsTrial,
            SubscriptionEndDate = baseDto.SubscriptionEndDate,
            IsSubscriptionActive = baseDto.IsSubscriptionActive,
            PlanType = baseDto.PlanType,
            BillingCycle = baseDto.BillingCycle,
            DaysRemaining = baseDto.DaysRemaining,
            DaysRemainingLabel = baseDto.DaysRemainingLabel,
            ScheduledNewPlanDays = baseDto.ScheduledNewPlanDays,
            TotalAccessDays = baseDto.TotalAccessDays,
            HasScheduledPlanActivation = baseDto.HasScheduledPlanActivation,
            PendingPlanEffectiveDate = baseDto.PendingPlanEffectiveDate,
            CancelAtPeriodEnd = baseDto.CancelAtPeriodEnd,
            SubscriptionStatusLabel = baseDto.SubscriptionStatusLabel,
            HasPendingPlanChange = baseDto.HasPendingPlanChange,
            HasPendingCheckout = baseDto.HasPendingCheckout,
            PendingPlanType = baseDto.PendingPlanType,
            PendingBillingCycle = baseDto.PendingBillingCycle,
            PendingPlanDisplayLabel = baseDto.PendingPlanDisplayLabel,
            GoogleEmail = baseDto.GoogleEmail,
            HasGoogleConnected = baseDto.HasGoogleConnected,
            AutoRenew = baseDto.AutoRenew,
            SectorID = baseDto.SectorID,
            BusinessHours = baseDto.BusinessHours,
            IsBlacklisted = tenant.IsBlacklisted,
            TrialUsed = tenant.TrialUsed,
            HasPendingPaymentToken = !string.IsNullOrWhiteSpace(tenant.SubscriptionReferenceCode)
        };
    }
}
