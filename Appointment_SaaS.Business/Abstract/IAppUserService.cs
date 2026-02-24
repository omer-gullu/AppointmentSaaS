using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract;

public interface IAppUserService
{
    Task<int> AddAppUserAsync(AppUserCreateDto dto);
    Task<List<AppUser>> GetAllUsersAsync();
    AppUser GetByMail(string email);
    List<OperationClaim> GetClaims(AppUser user);
}