using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AppUsersController : ControllerBase
{
    private readonly IAppUserService _appUserService;

    public AppUsersController(IAppUserService appUserService)
    {
        _appUserService = appUserService;
    }

    // 1. Yeni Kullanıcı (Personel/Müşteri) Ekle
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AppUserCreateDto dto)
    {
        // Manager tarafında Task<int> yaptığımız için yeni oluşan ID'yi alabiliyoruz
        var userId = await _appUserService.AddAppUserAsync(dto);

        return Ok(new
        {
            Message = "Kullanıcı başarıyla oluşturuldu.",
            AppUserID = userId
        });
    }

    // 2. Tüm Kullanıcıları Listele
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _appUserService.GetAllUsersAsync();
        return Ok(users);
    }
}