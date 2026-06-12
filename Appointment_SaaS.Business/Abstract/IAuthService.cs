using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
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
        Task <bool> UserExists(string email); // Kullanıcı daha önce kayıt olmuş mu?
        
        // Yeni OTP ve Business Entegrasyon Metotları
        Task<(int TenantId, string CheckoutFormScript)> RegisterBusinessOwnerAsync(BusinessRegistrationDto dto, string callbackUrl = "");
        Task<bool> VerifyPaymentAsync(string token);
        Task<bool> GenerateOtpForLoginAsync(OtpLoginDto dto);
        Task<AccessToken> VerifyOtpAndLoginAsync(OtpVerifyDto dto);
        Task UpdateAsync(AppUser user);
        Task DeleteAsync(AppUser user);
    }
}
