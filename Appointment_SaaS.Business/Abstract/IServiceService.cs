using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract;
public interface IServiceService
{
    Task<List<Service>> GetServicesByTenantIdAsync(int tenantId); // Dükkanın kendi hizmetleri
    Task<Service?> GetByIdAsync(int id);
    Task<int> AddServiceAsync(ServiceCreateDto dto);
    Task UpdateAsync(Service service);
    Task DeleteAsync(Service service);
}