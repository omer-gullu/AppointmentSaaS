using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.DataAccess.Abstract;
using AutoMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

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

    public async Task<int> AddAppUserAsync(AppUser user)
    {
        var users = _mapper.Map<AppUser>(user);

        await _userRepository.AddAsync(users);
        await _userRepository.SaveAsync();

        // Gerçek veritabanı ID'sini döndür
        return users.AppUserID;
    }

    public async Task<List<AppUser>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllAsync();
    }

    public async Task<AppUser> GetByMail(string email)
    {
        return await _userRepository
            .Where(u => u.Email != null && u.Email.ToLower() == email.ToLower())
            .FirstOrDefaultAsync();
    }

    public async Task<AppUser?> GetByPhoneNumberAsync(string phoneNumber)
    {
        static string Normalize(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return "";
            p = p.Trim();
            if (p.StartsWith("90") && p.Length > 10) p = p.Substring(2);
            if (p.StartsWith("0") && p.Length > 10) p = p.Substring(1);
            return p;
        }

        var normalized = Normalize(phoneNumber);

        // Veritabanı seviyesinde eşleştirme (Memory Leak önlendi)
        return await _userRepository
            .Where(u => u.PhoneNumber != null && 
                       (u.PhoneNumber == normalized || 
                        u.PhoneNumber == "0" + normalized || 
                        u.PhoneNumber == "90" + normalized))
            .FirstOrDefaultAsync();
    }

    public List<OperationClaim> GetClaims(AppUser user)
    {
        // Veritabanı katmanına (DataAccess) gidip kullanıcının rollerini getirir
        return _userRepository.GetClaims(user);
    }

    public async Task UpdateAsync(AppUser user)
    {
        // 1. Data katmanındaki IAppUserRepository içindeki Update metodunu çağırır (Senkron)
        _userRepository.Update(user);

        // 2. Değişiklikleri veritabanına fiziksel olarak yansıtır (Asenkron)
        await _userRepository.SaveAsync();
    }

    public async Task DeleteAsync(AppUser user)
    {
        // 3. Kullanıcıyı tamamen silmek yerine pasife çekmek (Soft Delete) daha güvenlidir
        user.Status = false;
        _userRepository.Update(user);

        // 4. Durum değişikliğini kaydet
        await _userRepository.SaveAsync();
    }

    public async Task<int> GetActiveStaffCountAsync(int tenantId)
    {
        return await _userRepository
            .Where(u => u.TenantID == tenantId && u.Status == true)
            .CountAsync();
    }

    public async Task<List<AppUser>> GetStaffByTenantAsync(int tenantId)
    {
        return await _userRepository
            .Where(u => u.TenantID == tenantId && u.Status == true)
            .ToListAsync();
    }
}