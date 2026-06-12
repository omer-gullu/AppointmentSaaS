using Appointment_SaaS.API.Authorization;
using Appointment_SaaS.Business.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.API.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("tenant/{tenantId:int}")]
    public async Task<IActionResult> GetTenantDashboard(int tenantId)
    {
        var denied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenantId);
        if (denied != null)
            return denied;

        var bundle = await _dashboardService.GetBundleAsync(tenantId);
        if (bundle == null)
            return NotFound(new { Message = "İşletme bulunamadı." });

        return Ok(bundle);
    }
}
