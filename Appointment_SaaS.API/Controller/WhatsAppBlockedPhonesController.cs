using Appointment_SaaS.API.Authorization;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WhatsAppBlockedPhonesController : ControllerBase
{
    private readonly ITenantBlockedPhoneService _blockedPhoneService;

    public WhatsAppBlockedPhonesController(ITenantBlockedPhoneService blockedPhoneService)
    {
        _blockedPhoneService = blockedPhoneService;
    }

    private int? GetCurrentTenantId()
    {
        var claim = User.FindFirst("TenantId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private bool IsAdmin() => User.IsInRole("Admin");

    [Authorize(Roles = "Admin,Manager")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null)
            return Unauthorized();

        return Ok(await _blockedPhoneService.GetByTenantAsync(tenantId.Value));
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpGet("by-tenant/{tenantId:int}")]
    public async Task<IActionResult> GetByTenant(int tenantId)
    {
        var denied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenantId);
        if (denied != null)
            return denied;

        return Ok(await _blockedPhoneService.GetByTenantAsync(tenantId));
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TenantBlockedPhoneCreateDto dto)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null)
            return Unauthorized();

        try
        {
            var created = await _blockedPhoneService.AddManualAsync(tenantId.Value, dto);
            return Ok(created);
        }
        catch (BadHttpRequestException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null)
            return Unauthorized();

        var removed = await _blockedPhoneService.RemoveAsync(tenantId.Value, id);
        if (!removed)
            return NotFound(new { Message = "Kayıt bulunamadı." });

        return Ok(new { Message = "Numara listeden kaldırıldı." });
    }

    /// <summary>n8n: mesaj gönderen gri listede mi?</summary>
    [AllowAnonymous]
    [HttpGet("check")]
    public async Task<IActionResult> Check(
        [FromQuery] string phone,
        [FromQuery] int tenantId = 0,
        [FromQuery] string? instanceName = null)
    {
        var resolved = await _blockedPhoneService.ResolveTenantIdAsync(tenantId, instanceName);
        if (resolved == null)
            return NotFound(new { Message = "İşletme bulunamadı." });

        if (string.IsNullOrWhiteSpace(phone))
            return BadRequest(new { Message = "phone parametresi gereklidir." });

        var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, resolved.Value);
        if (scopeDenied != null)
            return scopeDenied;

        var result = await _blockedPhoneService.IsBlockedAsync(resolved.Value, phone);
        return Ok(result);
    }

    /// <summary>n8n: "Asistanı kapat" sonrası numarayı gri listeye ekler.</summary>
    [AllowAnonymous]
    [HttpPost("opt-out")]
    public async Task<IActionResult> OptOut([FromBody] WhatsAppOptOutDto dto)
    {
        try
        {
            var resolvedTenant = await _blockedPhoneService.ResolveTenantIdAsync(dto.TenantId, dto.InstanceName);
            if (resolvedTenant == null)
                return NotFound(new { Message = "İşletme bulunamadı." });

            var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, resolvedTenant.Value);
            if (scopeDenied != null)
                return scopeDenied;

            var result = await _blockedPhoneService.OptOutAsync(dto);
            return Ok(new
            {
                Message = "Asistan bu numara için kapatıldı.",
                result.Blocked,
                result.PhoneCore
            });
        }
        catch (BadHttpRequestException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}
