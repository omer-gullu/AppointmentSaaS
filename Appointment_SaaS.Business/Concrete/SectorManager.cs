using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Appointment_SaaS.Business.Concrete;

public class SectorManager : ISectorService
{
    private readonly ISectorRepository _sectorRepository;
    private readonly IMapper _mapper;

    public SectorManager(ISectorRepository sectorRepository, IMapper mapper)
    {
        _sectorRepository = sectorRepository;
        _mapper = mapper;
    }

    public async Task<List<Sector>> GetAllAsync()
    {
        // Data katmanındaki repository üzerinden tüm sektörleri getirir
        return await _sectorRepository.GetAllAsync();
    }



    public async Task<int> AddAsync(SectorCreateDto dto)
    {
        var sector = _mapper.Map<Sector>(dto);
       

        await _sectorRepository.AddAsync(sector);
        await _sectorRepository.SaveAsync(); // İşte buraya aldık, Controller rahatladı!

        return sector.SectorID; // Yeni oluşan ID'yi döndük
    }

    public async Task UpdateAsync(Sector sector)
    {
        // 1. Repository'deki senkron Update metodunu çağırıyoruz
        _sectorRepository.Update(sector);

        // 2. Değişikliklerin DB'ye yansıması için asenkron SaveAsync şart
        await _sectorRepository.SaveAsync();
    }

    public async Task DeleteAsync(Sector sector)
    {
       
        _sectorRepository.Delete(sector);

        // 4. Silme işlemini onayla
        await _sectorRepository.SaveAsync();
    }
}