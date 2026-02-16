using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract;
public interface IServiceService
{
    Task<List<Service>> GetServicesByTenantIdAsync(int tenantId); // Dükkanın kendi hizmetleri
    Task<int> AddServiceAsync(ServiceCreateDto dto);
}