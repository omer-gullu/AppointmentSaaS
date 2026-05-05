using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract
{
    public interface IUserOperationClaimService
    {
        Task AddAsync(UserOperationClaim userOperationClaim);
        Task<List<UserOperationClaim>> GetClaimsByUserIdAsync(int userId);
    }
}
