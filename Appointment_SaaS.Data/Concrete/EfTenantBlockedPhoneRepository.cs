using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Context;

namespace Appointment_SaaS.Data.Concrete;

public class EfTenantBlockedPhoneRepository : GenericRepository<TenantBlockedPhone>, ITenantBlockedPhoneRepository
{
    public EfTenantBlockedPhoneRepository(AppDbContext context) : base(context) { }
}
