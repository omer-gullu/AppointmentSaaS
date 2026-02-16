using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantsController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpGet] // Tüm dükkanları listeler (Admin paneli için)
    public async Task<IActionResult> GetAll() => Ok(await _tenantService.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TenantCreateDto dto)
    {
        // Controller sadece "Ekle" der, nasıl ekleneceği Manager'ın işidir.
        var id = await _tenantService.AddTenantAsync(dto);
        

        return Ok(new { Status = "Başarılı", Id = id });
    }
}