using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Core.DTOs;
using AutoMapper;

namespace Appointment_SaaS.Business.Concrete;

public class AppointmentManager : IAppointmentService
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IMapper _mapper;

    public AppointmentManager(IAppointmentRepository appointmentRepository, IMapper mapper)
    {
        _appointmentRepository = appointmentRepository;
        _mapper = mapper;
    }



    public async Task<int> AddAppointmentAsync(AppointmentCreateDto dto)
    {
        // Tek satırda DTO'yu Entity'ye çeviriyoruz (MappingProfile'daki kuralı kullanır)
        var appointment = _mapper.Map<Appointment>(dto);

        //  Mapping'de olmayan veya otomatik hesaplanması gereken alanları ekle
        // Not: Eğer Profile'da EndDate için bir kural yazdıysan buraya da gerek kalmaz.
        appointment.EndDate = appointment.StartDate.AddMinutes(60);
        appointment.Status = "Beklemede";

        
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
