using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Data.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Appointment_SaaS.Business.Concrete;

public class AppointmentManager : IAppointmentService
{
    private readonly IGenericRepository<Appointment> _appointmentRepository;

    public AppointmentManager(IGenericRepository<Appointment> appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    public async Task AddAppointmentAsync(Appointment appointment)
    {
        // Burada ileride "Müşteriye SMS git" komutu da tetiklenebilir
        await _appointmentRepository.AddAsync(appointment);
        await _appointmentRepository.SaveAsync();
    }

    public async Task<List<Appointment>> GetAllByTenantIdAsync(int tenantId)
    {
        return await _appointmentRepository.Where(x => x.TenantID == tenantId).ToListAsync();
    }

    public async Task<bool> IsSlotAvailableAsync(int tenantId, DateTime date)
    {
        // Aynı dükkanda, aynı saatte başka randevu var mı kontrolü
        var exists = await _appointmentRepository.Where(x => x.TenantID == tenantId && x.StartDate == date).AnyAsync();
        return !exists;
    }
}
