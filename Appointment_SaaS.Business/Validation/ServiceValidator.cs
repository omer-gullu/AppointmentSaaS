using Appointment_SaaS.Core.DTOs;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Validation
{
    public class ServiceValidator : AbstractValidator<ServiceCreateDto>
    {
        public ServiceValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Hizmet adı (Örn: Saç Kesimi) boş olamaz.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Hizmet bedeli 0'dan büyük olmalıdır.");

            RuleFor(x => x.DurationMinutes)
                .InclusiveBetween(5, 300).WithMessage("Hizmet süresi 5 ile 300 dakika arasında olmalıdır.");
        }
    }
}
