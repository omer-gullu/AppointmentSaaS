using Appointment_SaaS.Core.DTOs;

namespace Appointment_SaaS.Business.Abstract;

public interface IDashboardService
{
    Task<DashboardBundleDto?> GetBundleAsync(int tenantId);
}
