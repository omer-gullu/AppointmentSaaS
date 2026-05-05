using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;

namespace Appointment_SaaS.DataAccess.Abstract;

public interface IAppUserRepository : IGenericRepository<AppUser>
{
    // Kullanıcıya özel (Email ile bulma vs.) metodlar buraya gelir

    List<OperationClaim> GetClaims(AppUser user);
}