using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Appointment_SaaS.Business.Abstract
{
    public interface ITenantService
    {
        Task<List<Tenant>> GetAllAsync();
        Task<Tenant?> GetByIdAsync(int id);
        Task<Tenant?> GetByApiKeyAsync(string apiKey);
        Task<Tenant?> GetByPhoneNumberAsync(string phoneNumber); // n8n webhook'undan dükkanı bulmak için
        Task<Tenant?> GetContextByInstanceAsync(string instanceName); // Mega Context API için
        Task<Tenant?> GetBySubscriptionReferenceAsync(string referenceCode); // Iyzico webhook için
        Task UpdateSubscriptionStatusAsync(Tenant tenant, bool isActive); // Iyzico webhook abonelik güncelleme için
        Task<int> AddTenantAsync(TenantCreateDto dto, string fingerprint);
        Task UpdateAsync(Tenant tenant);
        Task DeleteAsync(Tenant tenant);
        Task UpdateBusinessHoursAsync(int tenantId, List<BusinessHourDto> hours);

        // --- Anti-Fraud & Finansal Güvenlik ---
        /// <summary>Trial parmak izini kontrol et. Aynı karmaşık hash varsa null dönülmez.</summary>
        Task<Tenant?> GetByFingerprintAsync(string fingerprint);
        /// <summary>Webhook iade bildirimi: dükkanı anında askıya al + AuditLog/TransactionLog yaz.</summary>
        Task SuspendForRefundAsync(Tenant tenant, string? ipAddress, string? rawPayload, string? paymentId);
        /// <summary>Mükerrer iade suistimali: kara listeye al.</summary>
        Task BlacklistAsync(Tenant tenant, string reason);
    }
}
