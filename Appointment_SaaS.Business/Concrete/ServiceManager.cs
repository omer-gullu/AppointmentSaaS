using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Appointment_SaaS.Business.Concrete;

public class ServiceManager : IServiceService
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IMapper _mapper;

    public ServiceManager(IServiceRepository serviceRepository, IMapper mapper)
    {
        _serviceRepository = serviceRepository;
        _mapper = mapper;
    }

    public async Task<int> AddServiceAsync(ServiceCreateDto dto)
    {
        var service = _mapper.Map<Service>(dto);
      

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