using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.DataAccess.Abstract;
using AutoMapper;

namespace Appointment_SaaS.Business.Concrete;

public class AppUserManager : IAppUserService
{
    private readonly IAppUserRepository _userRepository;
    private readonly IMapper _mapper;

    public AppUserManager(IAppUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<int> AddAppUserAsync(AppUserCreateDto dto)
    {
        // Tek satırda dönüşüm:
        var user = _mapper.Map<AppUser>(dto);

        // Eğer veritabanında FirstName/LastName yerine sadece FullName tutuyorsan:
        // user.FullName = $"{dto.FirstName} {dto.LastName}";

        await _userRepository.AddAsync(user);
        await _userRepository.SaveAsync();

        return user.AppUserID;
    }

    public async Task<List<AppUser>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllAsync();
    }
}