using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.DataAccess.Abstract;

namespace Appointment_SaaS.Business.Concrete;

public class AppUserManager : IAppUserService
{
    private readonly IAppUserRepository _userRepository;

    public AppUserManager(IAppUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<int> AddAppUserAsync(AppUserCreateDto dto)
    {
        var user = new AppUser
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            TenantID = dto.TenantID, // SaaS yapısında dükkan eşleşmesi
            PasswordHash = "1234",
            Specialization = dto.Specialization,
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveAsync(); // Görsel 1'deki SaveAsync metodun
        return user.AppUserID;
    }

    public async Task<List<AppUser>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllAsync();
    }
}