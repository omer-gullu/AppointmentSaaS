using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Concrete
{
    public class AuditLogManager : IAuditLogService
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IAppUserService _appUserService;
        private readonly ITenantService _tenantService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogManager(
            IAuditLogRepository auditLogRepository,
            IAppUserService appUserService,
            ITenantService tenantService,
            IHttpContextAccessor httpContextAccessor)
        {
            _auditLogRepository = auditLogRepository;
            _appUserService = appUserService;
            _tenantService = tenantService;
            _httpContextAccessor = httpContextAccessor;
        }

        // ─── QUERY ────────────────────────────────────────────────────────────────

        public async Task<List<AuditLogDto>> GetAllLogsAsync()
            => await GetMappedLogsAsync(tenantId: null, source: null, level: null);

        public async Task<List<AuditLogDto>> GetLogsByTenantAsync(int tenantId)
            => await GetMappedLogsAsync(tenantId: tenantId, source: null, level: null);

        /// <summary>
        /// Kaynak bazlı filtreleme: "n8n", "API", "System" vb.
        /// </summary>
        public async Task<List<AuditLogDto>> GetLogsBySourceAsync(string source)
            => await GetMappedLogsAsync(tenantId: null, source: source, level: null);

        /// <summary>
        /// Seviye bazlı filtreleme: "Error", "Warning", "Info" vb.
        /// </summary>
        public async Task<List<AuditLogDto>> GetLogsByLevelAsync(string level)
            => await GetMappedLogsAsync(tenantId: null, source: null, level: level);

        // ─── WRITE ────────────────────────────────────────────────────────────────

        public async Task AddLogAsync(
            string action,
            string entityName,
            string entityId,
            string? oldValues = null,
            string? newValues = null,
            int? tenantId = null,
            int? userId = null)
        {
            var ctx = _httpContextAccessor.HttpContext;

            string? ipAddress = ctx?.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                             ?? ctx?.Connection.RemoteIpAddress?.ToString()
                             ?? "Bilinmiyor";

            if (!tenantId.HasValue && ctx != null)
            {
                var tenantClaim = ctx.User?.FindFirst("TenantId")?.Value;
                if (int.TryParse(tenantClaim, out int claimTenant))
                    tenantId = claimTenant;
            }

            if (!userId.HasValue && ctx != null)
            {
                var userIdClaim = ctx.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out int claimUser))
                    userId = claimUser;
            }

            var log = new AuditLog
            {
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldValues,
                NewValues = newValues,
                TenantId = tenantId,
                UserId = userId,
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };

            await _auditLogRepository.AddAsync(log);
            await _auditLogRepository.SaveAsync();
        }

        // ─── PRIVATE ──────────────────────────────────────────────────────────────

        private async Task<List<AuditLogDto>> GetMappedLogsAsync(
            int? tenantId,
            string? source,
            string? level)
        {
            var logs = await _auditLogRepository.GetLogsWithDetailsAsync(tenantId);

            // Source filtresi
            if (!string.IsNullOrWhiteSpace(source))
                logs = logs.Where(l => string.Equals(l.Source, source, StringComparison.OrdinalIgnoreCase)).ToList();

            // Level filtresi
            if (!string.IsNullOrWhiteSpace(level))
                logs = logs.Where(l => string.Equals(l.LogLevel, level, StringComparison.OrdinalIgnoreCase)).ToList();

            var uniqueUserIds = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId!.Value).Distinct().ToList();
            var uniqueTenantIds = logs.Where(l => l.TenantId.HasValue).Select(l => l.TenantId!.Value).Distinct().ToList();

            var users = await _appUserService.GetAllUsersAsync();
            var userDict = users.Where(u => uniqueUserIds.Contains(u.AppUserID))
                                 .ToDictionary(u => u.AppUserID, u => $"{u.FirstName} {u.LastName}");

            var tenants = await _tenantService.GetAllAsync();
            var tenantDict = tenants.Where(t => uniqueTenantIds.Contains(t.TenantID))
                                    .ToDictionary(t => t.TenantID, t => t.Name);

            return logs.Select(l => new AuditLogDto
            {
                AuditLogID = l.AuditLogID,
                Action = l.Action,
                EntityId = l.EntityId,
                EntityName = l.EntityName,
                IpAddress = l.IpAddress ?? "Bilinmiyor",
                NewValues = l.NewValues,
                OldValues = l.OldValues,
                Timestamp = l.Timestamp,
                TenantId = l.TenantId,
                UserId = l.UserId,
                UserFullName = l.UserId.HasValue && userDict.ContainsKey(l.UserId.Value)
                               ? userDict[l.UserId.Value] : "Sistem/Bilinmeyen",
                TenantName = l.TenantId.HasValue && tenantDict.ContainsKey(l.TenantId.Value)
                               ? tenantDict[l.TenantId.Value] : "Bilinmeyen"
            }).ToList();
        }
    }
}