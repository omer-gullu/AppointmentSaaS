using Appointment_SaaS.Core.DTOs;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Validation
{
    public class SectorValidator : AbstractValidator<SectorCreateDto>
    {
        public SectorValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Sektör adı boş olamaz (Örn: Berber, Klinik, Avukat).")
                .MinimumLength(3).WithMessage("Sektör adı en az 3 karakter olmalı.");
        }
    }
}
