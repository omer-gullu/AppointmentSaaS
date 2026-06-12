using Appointment_SaaS.Core.Constants;

namespace Appointment_SaaS.WebUI.Models
{
    public class DashboardViewModel
    {
        public List<dynamic> Appointments { get; set; } = new List<dynamic>();
        public List<dynamic> Services { get; set; } = new List<dynamic>();
        public List<dynamic> Staff { get; set; } = new List<dynamic>();
        public List<dynamic>? BusinessHours { get; set; }

        public bool BreakTimeEnabled { get; set; } = true;
        public string BreakStartTime { get; set; } = "12:00";
        public string BreakEndTime { get; set; } = "13:00";

        public string ShopName { get; set; }
        public string GoogleEmail { get; set; }
        public bool IsGoogleConnected => !string.IsNullOrEmpty(GoogleEmail);
        public string InstanceName { get; set; }
        public bool IsBotActive { get; set; } = true;
        public bool IsWhatsAppConnected { get; set; } = false;

        public string PlanType { get; set; } = "Trial";
        public string BillingCycle { get; set; } = BillingCycles.Monthly;
        public DateTime SubscriptionEndDate { get; set; }
        public bool IsTrial { get; set; }
        public string SubscriptionStatusLabel { get; set; } = string.Empty;
        public bool HasPendingPlanChange { get; set; }
        public bool HasPendingCheckout { get; set; }
        public bool HasScheduledPlanActivation { get; set; }
        public string? PendingPlanDisplayLabel { get; set; }
        public DateTime? PendingPlanEffectiveDate { get; set; }
        public bool CancelAtPeriodEnd { get; set; }

        public int DaysRemaining { get; set; }
        public string DaysRemainingLabel { get; set; } = string.Empty;
        public int? ScheduledNewPlanDays { get; set; }
        public int TotalAccessDays { get; set; }

        public string BillingCycleLabel =>
            BillingCycle == BillingCycles.Yearly ? "Yıllık" : "Aylık";

        public string PlanDisplayLabel =>
            IsTrial ? "Deneme" : $"{PlanType} / {BillingCycleLabel}";

        public int TodayAppointmentCount { get; set; }
        public int TotalAppointmentCount { get; set; }
        public int TotalServiceCount { get; set; }
        public int CurrentStaffCount { get; set; } = 0;

        public int StaffLimit => PlanPricing.GetStaffLimit(PlanType);

        public bool CanAddStaff => PlanPricing.CanUseStaff(PlanType);

        public bool CanUseReminders => PlanPricing.CanUseReminders(PlanType);

        public bool IsStaffLimitReached =>
            StaffLimit != -1 && CurrentStaffCount >= StaffLimit;

        public bool HasStaffOverLimit =>
            StaffLimit > 0 && CurrentStaffCount > StaffLimit;

        public string PlanBadge => PlanType?.ToLower() switch
        {
            "business" => "Business",
            "pro" => "Pro",
            "starter" => "Starter",
            _ => "Deneme"
        };
    }
}
