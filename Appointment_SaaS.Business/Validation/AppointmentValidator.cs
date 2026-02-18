using FluentValidation;
using Appointment_SaaS.Core.DTOs;

namespace Appointment_SaaS.Business.Validation;

public class AppointmentValidator : AbstractValidator<AppointmentCreateDto>
{
    public AppointmentValidator()
    {
        // Müşteri adı boş olamaz ve çok kısa olamaz
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Müşteri adı boş geçilemez.")
            .Length(3, 50).WithMessage("Müşteri adı 3 ile 50 karakter arasında olmalıdır.");

        // Randevu tarihi bugünden önce olamaz
        RuleFor(x => x.AppointmentDate)
            .GreaterThan(DateTime.Now).WithMessage("Geçmiş bir tarihe randevu veremezsin usta!");

        // Personel seçimi zorunlu
        RuleFor(x => x.AppUserID)
            .NotEmpty().WithMessage("Bu randevuyu hangi personel yönetecek? Seçim yapmalısın.");
    }
}