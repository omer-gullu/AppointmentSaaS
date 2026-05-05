using Appointment_SaaS.Core.Constants;
using System.Collections.Generic;

namespace Appointment_SaaS.WebUI.Models
{
    public class DashboardViewModel
    {
        public List<dynamic> Appointments { get; set; } = new List<dynamic>();
        public List<dynamic> Services { get; set; } = new List<dynamic>();
        public List<dynamic> Staff { get; set; } = new List<dynamic>();
        public List<dynamic>? BusinessHours { get; set; }

        public string ShopName { get; set; }
        public string GoogleEmail { get; set; }
        public bool IsGoogleConnected => !string.IsNullOrEmpty(GoogleEmail);
        public string InstanceName { get; set; }
        public bool IsBotActive { get; set; } = true;
        public bool IsWhatsAppConnected { get; set; } = false;

        // ?? Plan Bilgileri ????????????????????????????????????????????????????
        public string PlanType { get; set; } = "Trial";
        public DateTime SubscriptionEndDate { get; set; }

        // ?? �statistikler ?????????????????????????????????????????????????????
        public int TodayAppointmentCount { get; set; }
        public int TotalAppointmentCount { get; set; }
        public int TotalServiceCount { get; set; }
        public int CurrentStaffCount { get; set; } = 0;

        // ?? Plan Limitleri (Computed) ?????????????????????????????????????????

        /// <summary>
        /// Maksimum personel say�s�. -1 = s�n�rs�z, 0 = ekleyemez.
        /// PlanPricing.GetStaffLimit'ten beslenir.
        /// </summary>
        public int StaffLimit => PlanPricing.GetStaffLimit(PlanType);

        /// <summary>
        /// Personel ekleme �zelli�i bu planda a��k m�?
        /// Trial ? false, Starter/Business/Pro ? true
        /// </summary>
        public bool CanAddStaff => PlanPricing.CanUseStaff(PlanType);

        /// <summary>
        /// Hat�rlatma mesaj� �zelli�i bu planda a��k m�?
        /// Trial/Starter ? false, Business/Pro ? true
        /// </summary>
        public bool CanUseReminders => PlanPricing.CanUseReminders(PlanType);

        /// <summary>
        /// Personel limiti doldu mu?
        /// StaffLimit = -1 (s�n�rs�z) ise hi�bir zaman dolmaz.
        /// </summary>
        public bool IsStaffLimitReached =>
            StaffLimit != -1 && CurrentStaffCount >= StaffLimit;

        /// <summary>
        /// Dashboard'da g�sterilecek plan rozeti metni.
        /// </summary>
        public string PlanBadge => PlanType?.ToLower() switch
        {
            "business" => "?? Business",
            "pro" => "? Pro",
            "starter" => "?? Starter",
            _ => "?? Deneme"
        };
    }
}

