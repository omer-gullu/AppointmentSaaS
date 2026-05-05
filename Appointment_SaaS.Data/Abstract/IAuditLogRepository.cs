using Appointment_SaaS.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Appointment_SaaS.Data.Abstract
{
    public interface IAuditLogRepository : IGenericRepository<AuditLog>
    {
        Task<List<AuditLog>> GetLogsWithDetailsAsync(int? tenantId = null);
    }
}
