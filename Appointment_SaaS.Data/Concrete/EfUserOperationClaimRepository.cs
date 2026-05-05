using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Context;
using Appointment_SaaS.DataAccess.Abstract;

namespace Appointment_SaaS.Data.Concrete
{
    public class EfUserOperationClaimRepository : GenericRepository<UserOperationClaim>, IUserOperationClaimRepository
    {
        public EfUserOperationClaimRepository(AppDbContext context) : base(context)
        {
        }
    }
}
