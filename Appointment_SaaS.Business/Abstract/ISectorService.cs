using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Abstract;

public interface ISectorService
{
    Task<List<Sector>> GetAllAsync();
    Task AddAsync(Sector sector);
}