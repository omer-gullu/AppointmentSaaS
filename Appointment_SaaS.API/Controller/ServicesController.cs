using Appointment_SaaS.API.Authorization;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ServicesController : ControllerBase
{
    private readonly IServiceService _serviceService;
    private readonly ITenantService _tenantService;

    public ServicesController(IServiceService serviceService, ITenantService tenantService)
    {
        _serviceService = serviceService;
        _tenantService = tenantService;
    }

    // Task<int> AddServiceAsync(ServiceCreateDto dto) metodunu kullanır
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServiceCreateDto dto)
    {
        var enforce = ControllerTenantAccess.EnforceDtoTenantForManager(this, tid => dto.TenantID = tid);
        if (enforce != null)
            return enforce;

        var id = await _serviceService.AddServiceAsync(dto);
        return Ok(new { Message = "Hizmet başarıyla oluşturuldu", ServiceID = id });
    }

    // n8n: X-Auth-Token (WebhookAuthMiddleware) veya JWT
    [HttpGet("tenant/{tenantId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByTenantId(int tenantId)
    {
        var denied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenantId);
        if (denied != null)
            return denied;

        var result = await _serviceService.GetServicesByTenantIdAsync(tenantId);

        if (result == null || !result.Any())
            return NotFound("Bu dükkana ait herhangi bir hizmet bulunamadı.");

        return Ok(result);
    }

    /// <summary>n8n: X-Auth-Token veya JWT ile korunur.</summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id)
    {
        var service = await _serviceService.GetByIdAsync(id);
        if (service == null) return NotFound(new { Message = "Hizmet bulunamadı." });

        var denied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, service.TenantID);
        if (denied != null)
            return denied;

        return Ok(service);
    }

    /// <summary>n8n workflow: işletme numarasıyla hizmet listesi. X-Auth-Token veya JWT.</summary>
    [HttpGet("businessPhone/{phone}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByBusinessPhone(string phone)
    {
        var tenant = await _tenantService.GetByPhoneNumberAsync(phone);
        if (tenant == null)
            return NotFound(new { Message = "Bu numaraya ait işletme bulunamadı." });

        var denied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenant.TenantID);
        if (denied != null)
            return denied;

        var services = await _serviceService.GetServicesByTenantIdAsync(tenant.TenantID);
        
        if (services == null || !services.Any())
            return Ok(new List<object>()); // Boş liste dön, AI patlamasın

        // Sadece AI'ın anlayabileceği sade bir JSON dönüyoruz
        var aiFriendlyList = services.Select(s => new {
            ServiceID = s.ServiceID,
            Name = s.Name,
            DurationInMinutes = s.DurationInMinutes,
            Price = s.Price
        }).ToList();

        return Ok(aiFriendlyList);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ServiceCreateDto dto)
    {
        var service = await _serviceService.GetByIdAsync(id);
        if (service == null)
            return NotFound(new { Message = "Hizmet bulunamadı." });

        var denied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, service.TenantID);
        if (denied != null)
            return denied;

        service.Name = dto.Name;
        service.Price = dto.Price;
        service.DurationInMinutes = dto.DurationMinutes;

        await _serviceService.UpdateAsync(service);
        return Ok(new { Message = "Hizmet başarıyla güncellendi." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var service = await _serviceService.GetByIdAsync(id);
        if (service == null)
            return NotFound(new { Message = "Hizmet bulunamadı." });

        var denied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, service.TenantID);
        if (denied != null)
            return denied;

        await _serviceService.DeleteAsync(service);
        return Ok(new { Message = "Hizmet başarıyla silindi." });
    }
}