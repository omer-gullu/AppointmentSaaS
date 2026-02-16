using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Appointment_SaaS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SectorController : ControllerBase
{
    private readonly ISectorService _sectorService;

    public SectorController(ISectorService sectorService)
    {
        _sectorService = sectorService;
    }

    [HttpGet] // Tüm sektörleri listeler
    public async Task<IActionResult> GetAll()
    {
        var sectors = await _sectorService.GetAllAsync();
        return Ok(sectors);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SectorCreateDto dto)
    {
        // Artık _sectorService.SaveChanges() çağırmana GEREK YOK!
        // Manager zaten kendi içinde kaydedip bize ID dönüyor.
        var id = await _sectorService.AddAsync(dto);

        return Ok(new { Message = "Sektör başarıyla oluşturuldu", SectorID = id });
    }
}