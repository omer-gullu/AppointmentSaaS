using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    // 1. Randevu Oluşturma (Senin AddAppointmentAsync ve IsSlotAvailableAsync metodlarını kullanır)
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AppointmentCreateDto dto)
    {
        // Önce senin metodunla saati kontrol ediyoruz
        var isAvailable = await _appointmentService.IsSlotAvailableAsync(dto.TenantID, dto.AppointmentDate);

        if (!isAvailable)
        {
            return BadRequest(new { Message = "Seçtiğiniz tarih ve saatte başka bir randevu mevcut. Lütfen başka bir slot seçin." });
        }

        // Müsaitse randevuyu ekle ve oluşan ID'yi dön
        var appointmentId = await _appointmentService.AddAppointmentAsync(dto);

        return Ok(new
        {
            Message = "Randevu başarıyla onaylandı.",
            ID = appointmentId
        });
    }

    // 2. Dükkanın Tüm Randevularını Listeleme (Senin GetAllByTenantIdAsync metodunu kullanır)
    [HttpGet("tenant/{tenantId}")]
    public async Task<IActionResult> GetByTenant(int tenantId)
    {
        var appointments = await _appointmentService.GetAllByTenantIdAsync(tenantId);

        if (appointments == null || !appointments.Any())
        {
            return NotFound(new { Message = "Bu dükkan için henüz bir randevu kaydı bulunamadı." });
        }

        return Ok(appointments);
    }
}