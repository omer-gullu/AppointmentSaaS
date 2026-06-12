using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Utilities;
using FluentValidation;

namespace Appointment_SaaS.Business.Validation
{
    /// <summary>
    /// BusinessRegistrationDto için FluentValidation kuralları.
    /// Register formundaki tüm alanları kapsayan katmanlı doğrulama.
    /// Data Annotations üzerine güvenlik ağı görevi görür.
    /// NOT: TC Kimlik doğrulaması (MERNİS/NVİ) AuthManager.RegisterBusinessOwnerAsync içinde yapılır.
    /// </summary>
    public class BusinessRegistrationValidator : AbstractValidator<BusinessRegistrationDto>
    {
        public BusinessRegistrationValidator()
        {
            // ── KULLANICI BİLGİLERİ ──────────────────────────────────────

            RuleFor(x => x.UserFullName)
                .NotEmpty().WithMessage("Ad Soyad zorunludur.")
                .MustBeSafeName(minLength: 3, maxLength: 100)
                .Matches(@"^[\p{L}\s'-]+$").WithMessage("Ad Soyad yalnızca harf, boşluk ve tire içerebilir.");

            RuleFor(x => x.UserEmail)
                .NotEmpty().WithMessage("E-posta adresi zorunludur.")
                .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz. (Örn: isim@firma.com)")
                .MaximumLength(150).WithMessage("E-posta adresi en fazla 150 karakter olabilir.");

            RuleFor(x => x.IdentityNumber)
                .NotEmpty().WithMessage("T.C. Kimlik / Vergi Numarası zorunludur.")
                .Matches(@"^[0-9]{10,11}$").WithMessage("Geçerli bir T.C. Kimlik veya Vergi Numarası giriniz (10 veya 11 haneli).")
                .Must(TurkishIdentityValidator.IsValidTcOrVkn).WithMessage("Girdiğiniz kimlik veya vergi numarası geçersiz (Matematiksel algoritma hatası).");

            RuleFor(x => x.BirthYear)
                .NotEmpty().WithMessage("Doğum yılı zorunludur.")
                .InclusiveBetween(1900, DateTime.Now.Year - 18).WithMessage("18 yaşından büyük olmalısınız.");

            // ── TELEFON ────────────────────────────────────────────────────

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Telefon numarası zorunludur.")
                .Matches(@"^[0-9]{11}$").WithMessage("Telefon numarası tam 11 haneli rakamlardan oluşmalıdır. (Örn: 05551234567)");

            // ── İŞLETME BİLGİLERİ ───────────────────────────────────────

            RuleFor(x => x.BusinessName)
                .NotEmpty().WithMessage("İşletme adı zorunludur.")
                .MustBeSafeName(minLength: 2, maxLength: 150);

            RuleFor(x => x.SectorID)
                .GreaterThan(0).WithMessage("Lütfen bir sektör seçiniz.");

            RuleFor(x => x.Address)
                .MustBeSafeText(maxLength: 250);

            // ── PLAN BİLGİLERİ ──────────────────────────────────────────

            RuleFor(x => x.PlanType)
                .NotEmpty().WithMessage("Plan tipi zorunludur.")
                .Must(p => new[] { "trial", "Starter", "Business", "Pro" }
                    .Contains(p, StringComparer.OrdinalIgnoreCase))
                .WithMessage("Geçersiz plan tipi. (trial, Starter, Business veya Pro olmalıdır)");

            RuleFor(x => x.BillingCycle)
                .NotEmpty().WithMessage("Faturalandırma dönemi zorunludur.")
                .Must(c => c.Equals("Monthly", StringComparison.OrdinalIgnoreCase) ||
                           c.Equals("Yearly", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Faturalandırma dönemi Monthly veya Yearly olmalıdır.");
        }
    }
}
