using Appointment_SaaS.WebUI.Models;
using Microsoft.AspNetCore.Mvc;
using Appointment_SaaS.Business.Abstract;
using Microsoft.AspNetCore.Authorization;

namespace Appointment_SaaS.WebUI.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BusinessController : Controller
    {
        private readonly ITenantService _tenantService;
        private readonly IAuditLogService _auditLogService;

        public BusinessController(ITenantService tenantService, IAuditLogService auditLogService)
        {
            _tenantService = tenantService;
            _auditLogService = auditLogService;
        }

        // GET: /Business
        public async Task<IActionResult> Index()
        {
            var tenants = await _tenantService.GetAllAsync();
            return View(tenants);
        }

        // GET: /Business/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var tenant = await _tenantService.GetByIdAsync(id);
            if (tenant == null)
            {
                TempData["Error"] = "İşletme bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var model = new BusinessEditViewModel
            {
                TenantID = tenant.TenantID,
                Name = tenant.Name,
                InstanceName = tenant.InstanceName,
                PhoneNumber = tenant.PhoneNumber,
                Address = tenant.Address,
                IsActive = tenant.IsActive,
                IsTrial = tenant.IsTrial,
                SubscriptionEndDate = tenant.SubscriptionEndDate
            };

            return View(model);
        }

        // POST: /Business/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BusinessEditViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var tenant = await _tenantService.GetByIdAsync(id);
            if (tenant == null)
            {
                TempData["Error"] = "İşletme bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            tenant.Name = model.Name;
            tenant.PhoneNumber = model.PhoneNumber;
            tenant.Address = model.Address;
            tenant.InstanceName = model.InstanceName;
            tenant.IsActive = model.IsActive;
            tenant.IsTrial = model.IsTrial;
            tenant.SubscriptionEndDate = model.SubscriptionEndDate;

            await _tenantService.UpdateAsync(tenant);

            TempData["Success"] = $"'{model.Name}' işletmesi başarıyla güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Business/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string businessName)
        {
            var tenant = await _tenantService.GetByIdAsync(id);
            if (tenant == null)
            {
                TempData["Error"] = "İşletme bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            await _tenantService.DeleteAsync(tenant);

            TempData["Success"] = $"'{businessName}' işletmesi başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Business/GetAuditLogs/5
        [HttpGet]
        public async Task<IActionResult> GetAuditLogs(int id)
        {
            var logs = await _auditLogService.GetLogsByTenantAsync(id);
            return Json(logs);
        }
    }
}
