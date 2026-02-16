using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Microsoft.EntityFrameworkCore;

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



    public async Task<int> AddAsync(SectorCreateDto dto)
    {
        var sector = new Sector
        {
            Name = dto.Name,
            DefaultPrompt = dto.DefaultPrompt
        };

        await _sectorRepository.AddAsync(sector);
        await _sectorRepository.SaveAsync(); // İşte buraya aldık, Controller rahatladı!

        return sector.SectorID; // Yeni oluşan ID'yi döndük
    }
}