using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Utilities.Security.Jwt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Abstract
{
    public interface IAuthService
    {
        Task <AccessToken> Register(UserForRegisterDto userForRegisterDto, string password);
        AccessToken Login(UserForLoginDto userForLoginDto);
        bool UserExists(string email); // Kullanıcı daha önce kayıt olmuş mu?
    }
}
