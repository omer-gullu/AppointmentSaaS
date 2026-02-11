using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;

namespace Appointment_SaaS.Business.Concrete;

public class SectorManager : ISectorService
{
    private readonly ISectorRepository _sectorRepository;

    public SectorManager(ISectorRepository sectorRepository)
    {
        _sectorRepository = sectorRepository;
    }

    public async Task<List<Sector>> GetAllAsync()
    {
        // Data katmanındaki repository üzerinden tüm sektörleri getirir
        return await _sectorRepository.GetAllAsync();
    }

    public async Task AddAsync(Sector sector)
    {
        // Yeni sektörü ekler (Repository içinde SaveChanges olduğu için burada ekstradan gerek yok)
        await _sectorRepository.AddAsync(sector);
        await _sectorRepository.SaveAsync();
    }
}