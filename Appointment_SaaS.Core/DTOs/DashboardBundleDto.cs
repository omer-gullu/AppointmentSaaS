using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.Core.DTOs;

/// <summary>Dashboard için tek istekte dönen özet paket.</summary>
public class DashboardBundleDto
{
    public TenantResponseDto Tenant { get; set; } = null!;
    public List<AppointmentListItemDto> Appointments { get; set; } = new();
    public List<Service> Services { get; set; } = new();
    public List<StaffListItemDto> Staff { get; set; } = new();
}
