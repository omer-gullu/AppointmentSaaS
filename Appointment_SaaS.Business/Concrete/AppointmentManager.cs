using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Core.DTOs;

namespace Appointment_SaaS.Business.Concrete;

public class AppointmentManager : IAppointmentService
{
    private readonly IAppointmentRepository _appointmentRepository;

    public AppointmentManager(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }



    public async Task<int> AddAppointmentAsync(AppointmentCreateDto dto)
    {
        // Manager burada DTO'yu Entity'ye dönüştürür (Mapping)
        var appointment = new Appointment
        {
            TenantID = dto.TenantID,
            ServiceID = dto.ServiceID,
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            AppUserID = dto.AppUserID,
            StartDate = dto.AppointmentDate,
            EndDate = dto.AppointmentDate.AddMinutes(60), //
            Note = dto.Note,
            Status = "Beklemede"
        };

        await _appointmentRepository.AddAsync(appointment);
        await _appointmentRepository.SaveAsync();

        return appointment.AppointmentID;
    }

    public async Task<List<Appointment>> GetAllByTenantIdAsync(int tenantId)
    {
        var query = _appointmentRepository.Where(x => x.TenantID == tenantId);
        return await query.ToListAsync();
    }

    public async Task<bool> IsSlotAvailableAsync(int tenantId, DateTime date)
    {
        // Aynı dükkanda, aynı saatte başka randevu var mı kontrolü
        var exists = await _appointmentRepository.Where(x => x.TenantID == tenantId && x.StartDate == date).AnyAsync();
        return !exists;
    }
}
