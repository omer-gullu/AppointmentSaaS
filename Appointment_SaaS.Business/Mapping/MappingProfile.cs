using AutoMapper;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Appointment (Entity) -> AppointmentCreatedDto
        CreateMap<Appointment, AppointmentCreateDto>()
            .ForMember(dest => dest.AppointmentDate, opt => opt.MapFrom(src => src.StartDate));

        // AppointmentCreatedDto -> Appointment (Tersi için)
        CreateMap<AppointmentCreateDto, Appointment>()
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.AppointmentDate));

        CreateMap<TenantCreateDto, Tenant>()
    .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.Now))
    .ForMember(dest => dest.MessageCount, opt => opt.MapFrom(src => 0));

        // DTO -> Entity Dönüşümleri
        CreateMap<UserForRegisterDto, AppUser>();
        CreateMap<UserForRegisterDto, Tenant>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.FirstName));

        // AppUser
        CreateMap<AppUser, AppUserCreateDto>().ReverseMap();

        // Tenant

        // Service
        CreateMap<Service, ServiceCreateDto>().ReverseMap();

        // Sector
        CreateMap<Sector, SectorCreateDto>().ReverseMap();
    }
}