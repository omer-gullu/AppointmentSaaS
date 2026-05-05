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
      
        // İŞ MANTIĞI: Hizmet isminin ilk harflerini büyük yap (Örn: saç kesimi -> Saç Kesimi)
        if (!string.IsNullOrWhiteSpace(service.Name))
        {
            var trCulture = new System.Globalization.CultureInfo("tr-TR", false);
            service.Name = trCulture.TextInfo.ToTitleCase(service.Name.ToLower(trCulture));
        }
      

        await _serviceRepository.AddAsync(service);
        await _serviceRepository.SaveAsync(); // Kaydı kalıcı hale getiriyoruz

        return service.ServiceID; // Yeni ID'yi geri fırlatıyoruz
    }


    public async Task<List<Service>> GetServicesByTenantIdAsync(int tenantId)
    {
        // Önemli: Tüm hizmetleri çekip sadece o dükkana (Tenant) ait olanları filtreler
        return await _serviceRepository
                             .Where(x => x.TenantID == tenantId)
                             .AsNoTracking()
                             .ToListAsync();
    }

    public async Task<Service?> GetByIdAsync(int id)
    {
        return await _serviceRepository.Where(x => x.ServiceID == id).AsNoTracking().FirstOrDefaultAsync();
    }

    public async Task UpdateAsync(Service service)
    {
        // 1. Repository'deki senkron Update ile nesneyi işaretle
        _serviceRepository.Update(service);

        // 2. Değişiklikleri veritabanına asenkron olarak kaydet
        await _serviceRepository.SaveAsync();
    }

    public async Task DeleteAsync(Service service)
    {
        // 3. SaaS sistemlerinde hizmet silmek yerine pasife çekmek (Status = false)
        // randevu geçmişinin bozulmaması için kritik önem taşır.
        // Eğer tamamen silmek istersen:
        
        _serviceRepository.Delete(service);

        // 4. İşlemi onayla
        await _serviceRepository.SaveAsync();
    }
}