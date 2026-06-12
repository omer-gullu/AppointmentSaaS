using AutoMapper;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Business.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Appointment (Entity) -> AppointmentCreateDto
        CreateMap<Appointment, AppointmentCreateDto>();

        // AppointmentCreateDto -> Appointment (Tersi için)
        CreateMap<AppointmentCreateDto, Appointment>()
            .ForMember(dest => dest.AppointmentServiceLinks, opt => opt.Ignore())
            .ForMember(dest => dest.Service, opt => opt.Ignore())
            .ForMember(dest => dest.Tenant, opt => opt.Ignore())
            .ForMember(dest => dest.AppUser, opt => opt.Ignore())
            .ForMember(dest => dest.AppointmentID, opt => opt.Ignore());

        CreateMap<TenantCreateDto, Tenant>()
    .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.Now))
    .ForMember(dest => dest.MessageCount, opt => opt.MapFrom(src => 0));

        // DTO -> Entity Dönüşümleri
        CreateMap<UserForRegisterDto, AppUser>();
        CreateMap<UserForRegisterDto, Tenant>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.FirstName));


        // Tenant

        // Service
        CreateMap<ServiceCreateDto, Service>()
            .ForMember(dest => dest.DurationInMinutes, opt => opt.MapFrom(src => src.DurationMinutes));
        CreateMap<Service, ServiceCreateDto>();

        // Sector
        CreateMap<Sector, SectorCreateDto>().ReverseMap();
    }
}