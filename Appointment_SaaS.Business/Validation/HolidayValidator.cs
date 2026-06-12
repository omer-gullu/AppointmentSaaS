using Appointment_SaaS.Core.DTOs;
using FluentValidation;

namespace Appointment_SaaS.Business.Validation;

public class HolidayValidator : AbstractValidator<HolidayCreateDto>
{
    public HolidayValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tatil adı zorunludur.")
            .MustBeSafeName(minLength: 2, maxLength: 100);

        RuleFor(x => x.Date)
            .Must(d => d != default)
            .WithMessage("Geçerli bir tarih seçiniz.");
    }
}
