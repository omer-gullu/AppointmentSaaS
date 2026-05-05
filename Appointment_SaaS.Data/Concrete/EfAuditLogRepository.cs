using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Context;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Appointment_SaaS.Data.Concrete
{
    public class EfAuditLogRepository : GenericRepository<AuditLog>, IAuditLogRepository
    {
        private readonly AppDbContext _context;

        public EfAuditLogRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<AuditLog>> GetLogsWithDetailsAsync(int? tenantId = null)
        {
            var query = _context.AuditLogs.AsNoTracking().AsQueryable();

            if (tenantId.HasValue)
            {
                query = query.Where(x => x.TenantId == tenantId.Value);
            }

            return await query.OrderByDescending(x => x.Timestamp).ToListAsync();
        }
    }
}
