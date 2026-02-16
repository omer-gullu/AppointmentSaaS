using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ServicesController : ControllerBase
{
    private readonly IServiceService _serviceService;

    public ServicesController(IServiceService serviceService)
    {
        _serviceService = serviceService;
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
    public async Task<IActionResult> GetByTenantId(int tenantId)
    {
        var result = await _serviceService.GetServicesByTenantIdAsync(tenantId);

        if (result == null || !result.Any())
            return NotFound("Bu dükkana ait herhangi bir hizmet bulunamadı.");

        return Ok(result);
    }
}