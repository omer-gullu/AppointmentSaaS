using Appointment_SaaS.Core.DTOs;
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
                .MinimumLength(3).WithMessage("Ad Soyad en az 3 karakter olmalıdır.")
                .MaximumLength(100).WithMessage("Ad Soyad en fazla 100 karakter olabilir.")
                .Matches(@"^[\p{L}\s'-]+$").WithMessage("Ad Soyad yalnızca harf, boşluk ve tire içerebilir.");

            RuleFor(x => x.UserEmail)
                .NotEmpty().WithMessage("E-posta adresi zorunludur.")
                .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz. (Örn: isim@firma.com)")
                .MaximumLength(150).WithMessage("E-posta adresi en fazla 150 karakter olabilir.");

            // Format kontrolü burada, NVİ doğrulaması AuthManager'da yapılır
            RuleFor(x => x.IdentityNumber)
                .NotEmpty().WithMessage("T.C. Kimlik / Vergi Numarası zorunludur.")
                .Matches(@"^[0-9]{10,11}$").WithMessage("Geçerli bir T.C. Kimlik veya Vergi Numarası giriniz (10 veya 11 haneli).");

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
                .MinimumLength(2).WithMessage("İşletme adı en az 2 karakter olmalıdır.")
                .MaximumLength(150).WithMessage("İşletme adı en fazla 150 karakter olabilir.");

            RuleFor(x => x.SectorID)
                .GreaterThan(0).WithMessage("Lütfen bir sektör seçiniz.");

            RuleFor(x => x.Address)
                .MaximumLength(250).WithMessage("Adres en fazla 250 karakter olabilir.");

            // ── ÖDEME / KART BİLGİLERİ ──────────────────────────────────

            RuleFor(x => x.CardHolderName)
                .NotEmpty().WithMessage("Kart üzerindeki isim zorunludur.")
                .MinimumLength(3).WithMessage("Kart üzerindeki isim en az 3 karakter olmalıdır.")
                .MaximumLength(100).WithMessage("Kart üzerindeki isim en fazla 100 karakter olabilir.")
                .Matches(@"^[\p{L}\s]+$").WithMessage("Kart üzerindeki isim yalnızca harf ve boşluk içermelidir.");

            RuleFor(x => x.CardNumber)
                .NotEmpty().WithMessage("Kart numarası zorunludur.")
                .Matches(@"^[0-9]{16}$").WithMessage("Kart numarası tam 16 haneli rakamlardan oluşmalıdır.")
                .Must(BeValidLuhn).WithMessage("Geçersiz kart numarası. Lütfen kontrol ediniz.");

            RuleFor(x => x.ExpireMonth)
                .NotEmpty().WithMessage("Son kullanma ayı zorunludur.")
                .Matches(@"^(0[1-9]|1[0-2])$").WithMessage("Son kullanma ayı 01–12 arasında olmalıdır.");

            RuleFor(x => x.ExpireYear)
                .NotEmpty().WithMessage("Son kullanma yılı zorunludur.")
                .Matches(@"^\d{4}$").WithMessage("Son kullanma yılı 4 haneli olmalıdır.")
                .Must(year => int.TryParse(year, out var y) && y >= DateTime.Now.Year)
                    .WithMessage("Son kullanma yılı geçmiş bir tarih olamaz.");

            RuleFor(x => x.Cvc)
                .NotEmpty().WithMessage("CVV zorunludur.")
                .Matches(@"^[0-9]{3,4}$").WithMessage("CVV 3 veya 4 haneli olmalıdır.");

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

        /// <summary>
        /// Luhn algoritması ile kart numarasının geçerliliğini doğrular.
        /// Sahte veya rastgele girilmiş numaraları tespit eder.
        /// </summary>
        private static bool BeValidLuhn(string cardNumber)
        {
            if (string.IsNullOrWhiteSpace(cardNumber)) return false;

            var digits = cardNumber.Where(char.IsDigit).Select(c => c - '0').ToArray();
            if (digits.Length != 16) return false;

            int sum = 0;
            bool alternate = false;

            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int n = digits[i];
                if (alternate)
                {
                    n *= 2;
                    if (n > 9) n -= 9;
                }
                sum += n;
                alternate = !alternate;
            }

            return sum % 10 == 0;
        }
    }
}