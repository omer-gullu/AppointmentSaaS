using Appointment_SaaS.Core.DTOs;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Validation
{
    public class TenantValidator : AbstractValidator<TenantCreateDto>
    {
        public TenantValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("İşletme adı zorunludur.")
                .MustBeSafeName(minLength: 2, maxLength: 150);

            RuleFor(x => x.SectorID).NotEmpty().WithMessage("Bir sektör seçilmesi zorunludur.");

            RuleFor(x => x.Address).MustBeSafeText(maxLength: 250);
        }
    }
}
