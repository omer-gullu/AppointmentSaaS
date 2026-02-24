using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities.Security.Hashing;
using Appointment_SaaS.Core.Utilities.Security.Jwt;
using Appointment_SaaS.Core.Utilities.Security.JWT;
using AutoMapper;
using Humanizer;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Concrete
{
    public class AuthManager : IAuthService
    {
        private readonly IAppUserService _userService;
        private readonly ITenantService _tenantService;
        private readonly ITokenHelper _tokenHelper;
        private readonly IMapper _mapper; // Mapper eklendi

        public AuthManager(IAppUserService userService, ITenantService tenantService, ITokenHelper tokenHelper, IMapper mapper)
        {
            _userService = userService;
            _tenantService = tenantService;
            _tokenHelper = tokenHelper;
            _mapper = mapper;
        }

        public async Task<AccessToken> Register(UserForRegisterDto userForRegisterDto, string password)
        {
            // 1. ADIM: İşletmeyi (Tenant) oluşturuyoruz. 
            // İş mantığı (10 gün deneme süresi vb.) TenantManager.AddTenantAsync içinde çalışıyor.
            var tenantCreateDto = new TenantCreateDto { Name = userForRegisterDto.FirstName + " Business" };
            var tenantId = await _tenantService.AddTenantAsync(tenantCreateDto);

            // 2. ADIM: Şifreyi güvenli hale getiriyoruz (byte[] üretir)
            byte[] passwordHash, passwordSalt;
            HashingHelper.CreatePasswordHash(password, out passwordHash, out passwordSalt);

            // 3. ADIM: UserService'e göndermek üzere paketimizi (DTO) hazırlıyoruz
            var userCreateDto = new AppUserCreateDto
            {
                Email = userForRegisterDto.Email,
                FirstName = userForRegisterDto.FirstName,
                LastName = userForRegisterDto.LastName,
                PasswordHash = passwordHash, // DTO'da byte[] olarak güncellediğini varsayıyorum
                PasswordSalt = passwordSalt, // DTO'da byte[] olarak güncellediğini varsayıyorum
                TenantID = tenantId,
                Status = true
            };

            // Kullanıcıyı asenkron olarak kaydediyoruz
            await _userService.AddAppUserAsync(userCreateDto);

            // 4. ADIM: TOKEN OLUŞTURMA (Hataların çözüldüğü yer)
            // Token üretmek için DTO'yu değil, veritabanından asıl kullanıcı nesnesini (Entity) çekiyoruz.
            var userEntity =  _userService.GetByMail(userForRegisterDto.Email);

            // Artık 'userEntity' bir AppUser olduğu için metodun beklediği tipe tam uyuyor
            var claims = _userService.GetClaims(userEntity);
            var accessToken = _tokenHelper.CreateToken(userEntity, claims);

            return accessToken;
        }

        public AccessToken Login(UserForLoginDto userForLoginDto)
        {
            var userToCheck = _userService.GetByMail(userForLoginDto.Email);
            if (userToCheck == null) throw new Exception("Kullanıcı bulunamadı.");

            if (!HashingHelper.VerifyPasswordHash(userForLoginDto.Password, userToCheck.PasswordHash, userToCheck.PasswordSalt))
            {
                throw new Exception("Şifre hatalı.");
            }

            // Login için de rolleri alıyoruz
            var claims = _userService.GetClaims(userToCheck);
            return _tokenHelper.CreateToken(userToCheck, claims);
        }

        public bool UserExists(string email)
        {
            if (_userService.GetByMail(email) != null)
            {
                return false; // Kullanıcı zaten var
            }
            return true;
        }







        // Login ve UserExists metodları aynı kalabilir (Entity-DTO dönüşümü gerekmediği için)
    }
}
