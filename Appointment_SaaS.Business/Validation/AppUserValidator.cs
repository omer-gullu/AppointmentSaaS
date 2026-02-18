using Appointment_SaaS.Core.DTOs;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Validation
{
    public class AppUserValidator : AbstractValidator<AppUserCreateDto>
    {
        public AppUserValidator()
        {
         

            RuleFor(x => x.FirstName).NotEmpty().WithMessage("İsim zorunludur.");
            RuleFor(x => x.LastName).NotEmpty().WithMessage("Soyisim zorunludur.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("E-posta adresi boş geçilemez.")
                .EmailAddress().WithMessage("Lütfen geçerli bir e-posta formatı giriniz.");

            RuleFor(x => x.PasswordHash)
                .NotEmpty().WithMessage("Şifre zorunludur.")
                .MinimumLength(6).WithMessage("Şifre en az 6 karakter olmalıdır.");
        }
    }
}
