using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Appointment_SaaS.Business.Concrete;

public class ServiceManager : IServiceService
{
    private readonly IServiceRepository _serviceRepository;

    public ServiceManager(IServiceRepository serviceRepository)
    {
        _serviceRepository = serviceRepository;
    }

    public async Task<int> AddServiceAsync(ServiceCreateDto dto)
    {
        var service = new Service
        {
            Name = dto.Name,
            Price = dto.Price,
            DurationInMinutes = dto.DurationMinutes,
            TenantID = dto.TenantID
        };

        await _serviceRepository.AddAsync(service);
        await _serviceRepository.SaveAsync(); // Kaydı kalıcı hale getiriyoruz

        return service.ServiceID; // Yeni ID'yi geri fırlatıyoruz
    }

    public async Task<List<Service>> GetServicesByTenantIdAsync(int tenantId)
    {
        // Önemli: Tüm hizmetleri çekip sadece o dükkana (Tenant) ait olanları filtreler
        return await _serviceRepository
                             .Where(x => x.TenantID == tenantId)
                             .ToListAsync();
    }


}