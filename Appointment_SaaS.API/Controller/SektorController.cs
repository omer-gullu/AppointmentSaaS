using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SectorsController : ControllerBase
{
    private readonly ISectorService _sectorService;

    public SectorsController(ISectorService sectorService)
    {
        _sectorService = sectorService;
    }

    [HttpGet] // Tüm sektörleri listeler
    public async Task<IActionResult> GetAll()
    {
        var sectors = await _sectorService.GetAllAsync();
        return Ok(sectors);
    }

    [HttpPost] // Manuel sektör eklemek istersen (Admin gibi)
    public async Task<IActionResult> Create(Sector sector)
    {
        await _sectorService.AddAsync(sector);
        return Ok(sector);
    }
}