using Appointment_SaaS.Business.Abstract;
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

    [HttpPost] // Yeni Dükkan Kaydı
    public async Task<IActionResult> Create(Tenant tenant)
    {
        // Usta buraya dikkat: Kayıt anında benzersiz anahtarı ve sayacı kuruyoruz
        tenant.ApiKey = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(); // Kısa bir kod üretir
        tenant.CreatedAt = DateTime.Now;
        tenant.MessageCount = 0; // İlk mesajlar bedava başlasın
        tenant.IsBotActive = true; // Bot varsayılan olarak açık

        await _tenantService.AddTenantAsync(tenant);

        return Ok(new
        {
            Status = "Başarılı",
            Key = tenant.ApiKey,
            Details = tenant
        });
    }
}