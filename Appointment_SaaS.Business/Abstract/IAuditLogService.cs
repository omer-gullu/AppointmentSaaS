using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Abstract
{
    public interface IAuditLogService
    {
        Task<List<AuditLogDto>> GetAllLogsAsync();
        Task<List<AuditLogDto>> GetLogsByTenantAsync(int tenantId);
        Task<List<AuditLogDto>> GetLogsBySourceAsync(string source);
        Task<List<AuditLogDto>> GetLogsByLevelAsync(string level);

        /// <summary>
        /// Yeni bir audit log kaydı oluşturur.
        /// IP adresi ve TenantId otomatik olarak HTTP context'ten alınır.
        /// </summary>
        Task AddLogAsync(
            string action,
            string entityName,
            string entityId,
            string? oldValues = null,
            string? newValues = null,
            int? tenantId = null,
            int? userId = null);
    }
}