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
        var id = await _serviceService.AddServiceAsync(dto);
        return Ok(new { Message = "Hizmet başarıyla oluşturuldu", ServiceID = id });
    }

    // Task<List<Service>> GetServicesByTenantIdAsync(int tenantId) metodunu kullanır
    [HttpGet("tenant/{tenantId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByTenantId(int tenantId)
    {
        var result = await _serviceService.GetServicesByTenantIdAsync(tenantId);

        if (result == null || !result.Any())
            return NotFound("Bu dükkana ait herhangi bir hizmet bulunamadı.");

        return Ok(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id)
    {
        var service = await _serviceService.GetByIdAsync(id);
        if (service == null) return NotFound(new { Message = "Hizmet bulunamadı." });
        return Ok(service);
    }

    // YENİ: n8n Workflow başlarken işletme numarasıyla hizmetleri dinamik çekmek için!
    [HttpGet("businessPhone/{phone}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByBusinessPhone(string phone)
    {
        var tenant = await _tenantService.GetByPhoneNumberAsync(phone);
        if (tenant == null)
            return NotFound(new { Message = "Bu numaraya ait işletme bulunamadı." });

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

        await _serviceService.DeleteAsync(service);
        return Ok(new { Message = "Hizmet başarıyla silindi." });
    }
}