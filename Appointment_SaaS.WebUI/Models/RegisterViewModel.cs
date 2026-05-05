using System.ComponentModel.DataAnnotations;

namespace Appointment_SaaS.WebUI.Models
{
    public class RegisterViewModel
    {
        // Kullanıcı Bilgileri
        [Required(ErrorMessage = "Ad Soyad zorunludur.")]
        public string UserFullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz. (örn: isim@firma.com)")]
        public string UserEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "T.C. Kimlik / Vergi Numarası zorunludur.")]
        public string IdentityNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Doğum yılı zorunludur.")]
        public int BirthYear { get; set; }

        [Required(ErrorMessage = "Telefon numarası zorunludur.")]
        [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
        [RegularExpression(@"^[0-9]{11}$", ErrorMessage = "Telefon numarası tam 11 haneli olmalıdır. (Örn: 05551234567)")]
        public string PhoneNumber { get; set; } = string.Empty;

        // İşletme Bilgileri
        [Required(ErrorMessage = "İşletme adı zorunludur.")]
        public string BusinessName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Sektör seçimi zorunludur.")]
        public int SectorID { get; set; }

        public string Address { get; set; } = string.Empty;

        // Plan Bilgisi (pricing sayfasından taşınır)
        public string SelectedPlan { get; set; } = "trial"; // trial | starter | business | pro
        public string BillingCycle { get; set; } = "monthly"; // monthly | yearly

        // Kart Bilgileri — TÜM PLANLAR İÇİN ZORUNLU (Zero-Amount Checkout)
        [Required(ErrorMessage = "Kart üzerindeki isim zorunludur.")]
        public string CardHolderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kart numarası zorunludur.")]
        [RegularExpression(@"^[0-9]{16}$", ErrorMessage = "Kart numarası tam 16 haneli olmalıdır.")]
        public string CardNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Son kullanma ayı zorunludur.")]
        public string ExpireMonth { get; set; } = string.Empty;

        [Required(ErrorMessage = "Son kullanma yılı zorunludur.")]
        public string ExpireYear { get; set; } = string.Empty;

        [Required(ErrorMessage = "CVV zorunludur.")]
        public string Cvc { get; set; } = string.Empty;
    }
}
