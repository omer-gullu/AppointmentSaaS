using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract;

public interface IAppUserService
{
    Task<int> AddAppUserAsync(AppUser user);
    Task<int> GetActiveStaffCountAsync(int tenantId); // YENÝ
    Task<List<AppUser>> GetAllUsersAsync();
    Task<List<AppUser>> GetStaffByTenantAsync(int tenantId); // YENÝ
    Task<AppUser> GetByMail(string email);
    Task<AppUser?> GetByPhoneNumberAsync(string phoneNumber);
    List<OperationClaim> GetClaims(AppUser user);
    Task UpdateAsync(AppUser user);
    Task DeleteAsync(AppUser user);
}