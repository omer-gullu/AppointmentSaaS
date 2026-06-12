using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Concrete;
using Appointment_SaaS.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Appointment_SaaS.Data.Concrete;

public class EfAppointmentRepository : GenericRepository<Appointment>, IAppointmentRepository
{
    public EfAppointmentRepository(AppDbContext context) : base(context) { }

    public async Task<List<Appointment>> GetActiveByPhoneAsync(
        int tenantId,
        IReadOnlyCollection<string> phoneCandidates,
        DateTime nowLocal)
    {
        if (phoneCandidates == null || phoneCandidates.Count == 0)
            return new List<Appointment>();

        return await _context.Appointments
            .AsNoTracking()
            .Where(a => a.TenantID == tenantId
                     && a.EndDate >= nowLocal
                     && phoneCandidates.Contains(a.CustomerPhone))
            .Include(a => a.Service)
            .Include(a => a.AppointmentServiceLinks).ThenInclude(l => l.Service)
            .Include(a => a.AppUser)
            .OrderBy(a => a.StartDate)
            .ToListAsync();
    }
}
