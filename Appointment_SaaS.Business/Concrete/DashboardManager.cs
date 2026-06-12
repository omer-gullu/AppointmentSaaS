using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;

namespace Appointment_SaaS.Business.Concrete;

public class DashboardManager : IDashboardService
{
    private readonly ITenantService _tenantService;
    private readonly IAppointmentService _appointmentService;
    private readonly IServiceService _serviceService;
    private readonly IAppUserService _appUserService;

    public DashboardManager(
        ITenantService tenantService,
        IAppointmentService appointmentService,
        IServiceService serviceService,
        IAppUserService appUserService)
    {
        _tenantService = tenantService;
        _appointmentService = appointmentService;
        _serviceService = serviceService;
        _appUserService = appUserService;
    }

    public async Task<DashboardBundleDto?> GetBundleAsync(int tenantId)
    {
        var tenant = await _tenantService.GetByIdWithBusinessHoursAsync(tenantId);
        if (tenant == null)
            return null;

        var appointments = await _appointmentService.GetListItemsByTenantIdAsync(tenantId);
        var services = await _serviceService.GetServicesByTenantIdAsync(tenantId);
        var staff = await _appUserService.GetStaffListItemsByTenantAsync(tenantId);

        return new DashboardBundleDto
        {
            Tenant = TenantResponseDto.FromEntity(tenant),
            Appointments = appointments,
            Services = services,
            Staff = staff
        };
    }
}
