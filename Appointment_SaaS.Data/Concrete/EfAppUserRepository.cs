using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Context;
using Appointment_SaaS.DataAccess.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Data.Concrete
{
    public class EfAppUserRepository : GenericRepository<AppUser>, IAppUserRepository
    {
        public EfAppUserRepository(AppDbContext context) : base(context) { }
    }
}
