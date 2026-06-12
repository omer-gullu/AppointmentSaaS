using Appointment_SaaS.WebUI.Models;
using Appointment_SaaS.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appointment_SaaS.WebUI.Controllers;

[Authorize(Roles = "Manager,Admin")]
public class BusinessSettingsController : Controller
{
    private readonly IDashboardApiService _dashboardService;
    private readonly IWhatsAppBlockedPhoneApiService _blockedPhoneApiService;

    public BusinessSettingsController(
        IDashboardApiService dashboardService,
        IWhatsAppBlockedPhoneApiService blockedPhoneApiService)
    {
        _dashboardService = dashboardService;
        _blockedPhoneApiService = blockedPhoneApiService;
    }

    private int GetCurrentTenantId()
    {
        var tenantIdClaim = User.FindFirst("TenantId")?.Value;
        if (int.TryParse(tenantIdClaim, out int tenantId))
            return tenantId;
        throw new UnauthorizedAccessException("Giriş yapmanız gerekmektedir.");
    }

    private async Task<DashboardViewModel> LoadDashboardAsync()
    {
        return await _dashboardService.GetDashboardDataAsync(GetCurrentTenantId());
    }

    [HttpGet]
    public async Task<IActionResult> Holidays()
    {
        return View(await LoadDashboardAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Staff()
    {
        return View(await LoadDashboardAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Services()
    {
        return View(await LoadDashboardAsync());
    }

    [HttpGet]
    public async Task<IActionResult> BlockedPhones()
    {
        var (items, error) = await _blockedPhoneApiService.GetListAsync();
        if (!string.IsNullOrEmpty(error))
            TempData["Error"] = error;
        return View(new BlockedPhonesViewModel { Items = items });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBlockedPhone(BlockedPhonesViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.NewPhone))
        {
            TempData["Error"] = "Telefon numarası gereklidir.";
            return RedirectToAction(nameof(BlockedPhones));
        }

        var (success, message, _) = await _blockedPhoneApiService.AddAsync(model.NewPhone.Trim(), model.NewNote);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(BlockedPhones));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBlockedPhone(int id)
    {
        var (success, message) = await _blockedPhoneApiService.DeleteAsync(id);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(BlockedPhones));
    }
}
