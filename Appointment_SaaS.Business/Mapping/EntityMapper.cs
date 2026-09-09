using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Mapping;

/// <summary>
/// DTO ↔ entity dönüşümleri. AutoMapper (GHSA-rvv3-g6hj-g44x / ücretli patch)
/// yerine açık, sığ map — derin özyineleme DoS yüzeyi yok.
/// </summary>
public static class EntityMapper
{
    public static Appointment ToAppointment(AppointmentCreateDto dto) => new()
    {
        TenantID = dto.TenantID,
        ServiceID = dto.ServiceID,
        AppUserID = dto.AppUserID ?? 0,
        CustomerName = dto.CustomerName,
        CustomerPhone = dto.CustomerPhone,
        StartDate = dto.StartDate,
        EndDate = dto.EndDate,
        Note = dto.Note ?? string.Empty,
        GoogleEventID = dto.GoogleEventID,
        Status = string.Empty,
        IsConfirmed = false
    };

    public static Tenant ToTenant(TenantCreateDto dto) => new()
    {
        Name = dto.Name,
        SectorID = dto.SectorID,
        PhoneNumber = dto.PhoneNumber ?? string.Empty,
        InstanceName = dto.InstanceName,
        Address = dto.Address ?? string.Empty,
        CreatedAt = DateTime.Now,
        MessageCount = 0
    };

    public static Service ToService(ServiceCreateDto dto) => new()
    {
        Name = dto.Name,
        Price = dto.Price,
        DurationInMinutes = dto.DurationMinutes,
        TenantID = dto.TenantID
    };

    public static Sector ToSector(SectorCreateDto dto) => new()
    {
        Name = dto.Name,
        DefaultPrompt = dto.DefaultPrompt
    };
}
