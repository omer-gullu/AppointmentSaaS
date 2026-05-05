namespace Appointment_SaaS.Core.Constants
{
    /// <summary>
    /// SaaS planları için tek kaynak (Single Source of Truth) fiyatlandırma sabitleri.
    /// UI, Register ekranı ve API bu dosyadan beslenir.
    /// </summary>
    public static class PlanPricing
    {
        // ── Aylık Fiyatlandırma ──────────────────────────────────────────────────
        public const decimal StarterMonthly = 990;
        public const decimal ProMonthly = 1990;
        public const decimal BusinessMonthly = 3490;

        // ── Yıllık Fiyatlandırma (%20 indirimli yuvarlanmış tutarlar) ────────────
        public const decimal StarterYearly = 9504;
        public const decimal ProYearly = 19104;
        public const decimal BusinessYearly = 33504;

        // ── Plan Limitleri ───────────────────────────────────────────────────────
        public const int StarterStaffLimit = 1;
        public const int ProStaffLimit = 3;

        /// <summary>
        /// Seçilen plan ve döngüye göre dinamik fiyatı döndürür.
        /// </summary>
        public static decimal GetPrice(string planType, string billingCycle)
        {
            var key = $"{planType}_{billingCycle}".ToLower();
            return key switch
            {
                "starter_monthly" => StarterMonthly,
                "starter_yearly" => StarterYearly,
                "business_monthly" => BusinessMonthly,
                "business_yearly" => BusinessYearly,
                "pro_monthly" => ProMonthly,
                "pro_yearly" => ProYearly,
                _ => 0
            };
        }

        /// <summary>
        /// Plana göre maksimum personel sayısını döndürür.
        /// Trial: 0 (ekleyemez), Starter: 1, Pro: 3, Business: sınırsız (-1)
        /// </summary>
        public static int GetStaffLimit(string? planType)
        {
            return (planType ?? "Trial").ToLower() switch
            {
                "business" => -1,
                "pro" => ProStaffLimit,
                "starter" => StarterStaffLimit,
                "trial" => 1,
                _ => 1
            };
        }

        /// <summary>
        /// Bu plan personel ekleme özelliğini kullanabilir mi?
        /// Starter, Business ve Pro kullanabilir. Trial kullanamaz.
        /// </summary>
        public static bool CanUseStaff(string? planType)
        {
            var plan = (planType ?? "Trial").ToLower();
            return plan == "starter" || plan == "business" || plan == "pro" || plan == "trial";
        }

        /// <summary>
        /// Bu plan hatırlatma mesajı özelliğini kullanabilir mi?
        /// Sadece Pro ve Business kullanabilir.
        /// </summary>
        public static bool CanUseReminders(string? planType)
        {
            var plan = (planType ?? "Trial").ToLower();
            return plan == "pro" || plan == "business";
        }

        /// <summary>
        /// Yıllık planlarda kullanıcının gördüğü "Aylık" bazdaki indirimli fiyat.
        /// </summary>
        public static decimal GetMonthlyEquivalentForYearly(decimal yearlyPrice)
            => Math.Round(yearlyPrice / 12, 0);
    }
}