using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.DataAccess.Abstract;

namespace Appointment_SaaS.Business.Concrete
{
    public class UserOperationClaimManager : IUserOperationClaimService
    {
        private readonly IUserOperationClaimRepository _userOperationClaimRepository;

        public UserOperationClaimManager(IUserOperationClaimRepository userOperationClaimRepository)
        {
            _userOperationClaimRepository = userOperationClaimRepository;
        }

        public async Task AddAsync(UserOperationClaim userOperationClaim)
        {
            await _userOperationClaimRepository.AddAsync(userOperationClaim);
            await _userOperationClaimRepository.SaveAsync();
        }

        public async Task<List<UserOperationClaim>> GetClaimsByUserIdAsync(int userId)
        {
            var claims = await _userOperationClaimRepository.GetAllAsync();
            return claims.Where(x => x.UserId == userId).ToList();
        }
    }
}
