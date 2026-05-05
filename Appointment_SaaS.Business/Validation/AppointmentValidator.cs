using FluentValidation;
using Appointment_SaaS.Core.DTOs;

namespace Appointment_SaaS.Business.Validation;

/// <summary>
/// n8n ve WhatsApp'tan gelen randevu verilerini doğrular ve sanitize eder.
/// XSS, injection saldırılarına karşı koruma sağlar.
/// </summary>
public class AppointmentValidator : AbstractValidator<AppointmentCreateDto>
{
    public AppointmentValidator()
    {
        // Müşteri adı: boş olamaz, uzunluk kontrolü, XSS/injection kontrolü
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Müşteri adı boş geçilemez.")
            .Length(2, 60).WithMessage("Müşteri adı 2 ile 60 karakter arasında olmalıdır.")
            .Must(name => !WhatsAppInputSanitizer.ContainsXss(name))
                .WithMessage("Müşteri adında geçersiz karakterler tespit edildi.")
            .Must(name => !WhatsAppInputSanitizer.ContainsSqlInjection(name))
                .WithMessage("Müşteri adında güvenlik ihlali tespit edildi.");

        // Müşteri telefon numarası: format kontrolü
        RuleFor(x => x.CustomerPhone)
            .NotEmpty().WithMessage("Müşteri telefon numarası boş geçilemez.")
            .Must(phone => WhatsAppInputSanitizer.IsValidTurkishPhone(phone))
                .WithMessage("Geçerli bir Türk telefon numarası giriniz. (Örn: 05551112233 veya +905551112233)")
            .Must(phone => !WhatsAppInputSanitizer.ContainsSqlInjection(phone))
                .WithMessage("Telefon numarasında güvenlik ihlali tespit edildi.");

        // Başlangıç tarihi: geçmiş olmamalı
        RuleFor(x => x.StartDate)
            .GreaterThan(DateTime.Now.AddMinutes(-5))
                .WithMessage("Geçmiş bir tarihe randevu veremezsiniz!");

        // Bitiş tarihi: başlangıçtan sonra olmalı
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
                .WithMessage("Randevunun bitişi başlangıcından önce olamaz!")
            .When(x => x.EndDate != default); // EndDate controller tarafından set ediliyorsa bu kontrolü atla

        // Not alanı: isteğe bağlı ama XSS/injection kontrolü
        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Not alanı en fazla 500 karakter olabilir.")
            .Must(note => string.IsNullOrEmpty(note) || !WhatsAppInputSanitizer.ContainsXss(note))
                .WithMessage("Not alanında geçersiz HTML/Script içeriği tespit edildi.")
            .Must(note => string.IsNullOrEmpty(note) || !WhatsAppInputSanitizer.ContainsSqlInjection(note))
                .WithMessage("Not alanında güvenlik ihlali tespit edildi.")
            .When(x => x.Note != null);

        // ServiceID: geçerli olmalı
        RuleFor(x => x.ServiceID)
            .GreaterThan(0).WithMessage("Geçerli bir hizmet seçilmelidir.");

        // BusinessPhone: işletme numarası boş olamaz
        RuleFor(x => x.BusinessPhone)
            .NotEmpty().WithMessage("İşletme telefon numarası gereklidir.")
            .Must(phone => !WhatsAppInputSanitizer.ContainsSqlInjection(phone))
                .WithMessage("İşletme telefon numarasında güvenlik ihlali tespit edildi.");
    }
}
