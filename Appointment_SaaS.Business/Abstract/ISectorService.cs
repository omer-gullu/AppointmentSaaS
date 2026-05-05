using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract;

public interface ISectorService
{
    Task<List<Sector>> GetAllAsync();
    Task<int> AddAsync(SectorCreateDto dto);
    Task UpdateAsync(Sector sector);
    Task DeleteAsync(Sector sector);

}