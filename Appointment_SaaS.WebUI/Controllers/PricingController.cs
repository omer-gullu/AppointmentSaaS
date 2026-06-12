using Appointment_SaaS.WebUI.Models;
using Appointment_SaaS.WebUI.Services;
using Appointment_SaaS.WebUI.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace Appointment_SaaS.WebUI.Controllers
{
    public class PricingController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IDashboardApiService _dashboardApiService;

        public PricingController(
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor,
            IDashboardApiService dashboardApiService)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _dashboardApiService = dashboardApiService;
        }

        [HttpGet]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Roles = "Manager,Admin")]
        [HttpGet]
        public async Task<IActionResult> ChangePlan()
        {
            var tenantIdClaim = User.FindFirst("TenantId")?.Value;
            if (!int.TryParse(tenantIdClaim, out var tenantId))
            {
                TempData["ErrorMessage"] = "Oturum bilgisi bulunamadı.";
                return RedirectToAction("Login", "Auth");
            }

            var model = new ChangePlanViewModel { TenantId = tenantId };
            var client = await CreateApiClientAsync();
            var tenantRes = await client.GetAsync($"api/Tenants/{tenantId}");
            if (tenantRes.IsSuccessStatusCode)
            {
                var tenant = JsonSerializer.Deserialize<JsonElement>(
                    await tenantRes.Content.ReadAsStringAsync(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                model.CurrentPlanType = tenant.TryGetProperty("planType", out var p) ? p.GetString() ?? "Trial" : "Trial";
                model.CurrentBillingCycle = tenant.TryGetProperty("billingCycle", out var c) ? c.GetString() ?? "Monthly" : "Monthly";
                model.IsTrial = tenant.TryGetProperty("isTrial", out var t) && t.GetBoolean();
                model.CancelAtPeriodEnd = tenant.TryGetProperty("cancelAtPeriodEnd", out var cancelEnd) && cancelEnd.GetBoolean();
                model.IsSubscriptionActive = tenant.TryGetProperty("isSubscriptionActive", out var subActive) && subActive.GetBoolean();
                if (tenant.TryGetProperty("subscriptionEndDate", out var endDate)
                    && endDate.ValueKind != JsonValueKind.Null
                    && DateTime.TryParse(endDate.GetString(), out var parsedEnd))
                {
                    model.SubscriptionEndDate = parsedEnd;
                }
                model.PendingPlanDisplayLabel = tenant.TryGetProperty("pendingPlanDisplayLabel", out var pendingLbl)
                    ? pendingLbl.GetString()
                    : null;
            }

            var staffRes = await client.GetAsync($"api/AppUsers/staff/{tenantId}");
            if (staffRes.IsSuccessStatusCode)
            {
                var staff = JsonSerializer.Deserialize<List<JsonElement>>(await staffRes.Content.ReadAsStringAsync());
                model.CurrentStaffCount = staff?.Count ?? 0;
            }

            if (TempData["ChangePlanError"] is string err)
                model.ErrorMessage = err;
            if (TempData["ChangePlanSuccess"] is string ok)
                model.SuccessMessage = ok;
            if (TempData["ChangePlanCancelSuccess"] is string cancelOk)
                model.CancelSuccessMessage = cancelOk;
            if (TempData["CheckoutFormContent"] is string checkout)
                model.CheckoutFormContent = checkout;

            return View(model);
        }

        [Authorize(Roles = "Manager,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSubscriptionForChangePlan()
        {
            var tenantIdClaim = User.FindFirst("TenantId")?.Value;
            if (!int.TryParse(tenantIdClaim, out var tenantId))
            {
                TempData["ChangePlanError"] = "Oturum bilgisi bulunamadı.";
                return RedirectToAction(nameof(ChangePlan));
            }

            var (success, msg) = await _dashboardApiService.CancelSubscriptionAsync(tenantId);
            if (success)
                TempData["ChangePlanCancelSuccess"] = msg;
            else
                TempData["ChangePlanError"] = msg;

            return RedirectToAction(nameof(ChangePlan));
        }

        [Authorize(Roles = "Manager,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePlan(string targetPlanType, string targetBillingCycle)
        {
            var tenantIdClaim = User.FindFirst("TenantId")?.Value;
            if (!int.TryParse(tenantIdClaim, out var tenantId))
            {
                TempData["ChangePlanError"] = "Oturum bilgisi bulunamadı.";
                return RedirectToAction("Login", "Auth");
            }

            var client = await CreateApiClientAsync();
            var tenantRes = await client.GetAsync($"api/Tenants/{tenantId}");
            if (tenantRes.IsSuccessStatusCode)
            {
                var tenant = JsonSerializer.Deserialize<JsonElement>(
                    await tenantRes.Content.ReadAsStringAsync(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var isTrial = tenant.TryGetProperty("isTrial", out var t) && t.GetBoolean();
                var cancelAtPeriodEnd = tenant.TryGetProperty("cancelAtPeriodEnd", out var c) && c.GetBoolean();
                var isSubscriptionActive = tenant.TryGetProperty("isSubscriptionActive", out var sa) && sa.GetBoolean();

                if (!isTrial && isSubscriptionActive && !cancelAtPeriodEnd)
                {
                    TempData["ChangePlanError"] =
                        "Yeni plan seçmeden önce mevcut aboneliğinizin otomatik yenilenmesini iptal etmeniz gerekir.";
                    return RedirectToAction(nameof(ChangePlan));
                }
            }

            var callbackUrl = Url.Action("PaymentCallback", "Auth", null, Request.Scheme, Request.Host.Value)
                ?? $"{Request.Scheme}://{Request.Host}/Auth/PaymentCallback";

            var payload = new
            {
                targetPlanType,
                targetBillingCycle,
                paymentCallbackUrl = callbackUrl
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("api/Tenants/change-plan/init", content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var err = JsonSerializer.Deserialize<JsonElement>(body);
                    TempData["ChangePlanError"] = err.TryGetProperty("message", out var m) ? m.GetString() : "Plan değişikliği başlatılamadı.";
                }
                catch
                {
                    TempData["ChangePlanError"] = "Plan değişikliği başlatılamadı.";
                }
                return RedirectToAction(nameof(ChangePlan));
            }

            var result = JsonSerializer.Deserialize<JsonElement>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var mode = result.TryGetProperty("mode", out var modeEl) ? modeEl.GetString() : "checkout";

            if (string.Equals(mode, "upgrade", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ChangePlanSuccess"] = result.TryGetProperty("message", out var msg) ? msg.GetString() : "Planınız güncellendi.";
                return RedirectToAction("Index", "Dashboard");
            }

            if (result.TryGetProperty("checkoutFormContent", out var checkout) && checkout.ValueKind == JsonValueKind.String)
            {
                TempData["CheckoutFormContent"] = checkout.GetString();
                return RedirectToAction(nameof(ChangePlan));
            }

            TempData["ChangePlanError"] = "Ödeme formu alınamadı.";
            return RedirectToAction(nameof(ChangePlan));
        }

        [Authorize]
        [HttpGet]
        public IActionResult Upgrade(string plan, string cycle = "monthly")
        {
            return RedirectToAction(nameof(ChangePlan));
        }

        private async Task<HttpClient> CreateApiClientAsync()
        {
            var client = _httpClientFactory.CreateClient("Api");
            await HttpClientTokenHelper.AttachBearerTokenAsync(client, _httpContextAccessor);
            return client;
        }
    }
}
