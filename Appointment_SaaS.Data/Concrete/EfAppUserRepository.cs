using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Context;
using Appointment_SaaS.DataAccess.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NuGet.Protocol.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OperationClaim = Appointment_SaaS.Core.Entities.OperationClaim;


namespace Appointment_SaaS.Data.Concrete
{
    public class EfAppUserRepository : GenericRepository<AppUser>, IAppUserRepository
    {
        private readonly AppDbContext _context; // Klasör olan Context ile karışmaması için bunu ekliyoruz

        public EfAppUserRepository(AppDbContext context) : base(context)
        {
            _context = context; // Gelen context'i yerel değişkene hapsediyoruz
        }

        public List<OperationClaim> GetClaims(AppUser user)
        {
            var result = from operationClaim in _context.OperationClaims
                         join userOperationClaim in _context.UserOperationClaims
                             on operationClaim.Id equals userOperationClaim.OperationClaimId
                         where userOperationClaim.Id == user.AppUserID
                         select new OperationClaim
                         {
                             Id = operationClaim.Id,
                             Name = operationClaim.Name
                         };
            return result.ToList();
        }

    }
}
