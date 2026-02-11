using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;

namespace Appointment_SaaS.Business.Concrete;

public class ServiceManager : IServiceService
{
    private readonly IServiceRepository _serviceRepository;

    public ServiceManager(IServiceRepository serviceRepository)
    {
        _serviceRepository = serviceRepository;
    }

    public async Task AddServiceAsync(Service service)
    {
        // Yeni hizmeti (Örn: Saç Kesim) veritabanına ekler
        await _serviceRepository.AddAsync(service);
    }

    public async Task<List<Service>> GetServicesByTenantIdAsync(int tenantId)
    {
        // Önemli: Tüm hizmetleri çekip sadece o dükkana (Tenant) ait olanları filtreler
        var allServices = await _serviceRepository.GetAllAsync();
        return allServices.Where(x => x.TenantID == tenantId).ToList();
    }
}