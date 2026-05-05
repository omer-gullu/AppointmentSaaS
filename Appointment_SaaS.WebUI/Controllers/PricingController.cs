using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Appointment_SaaS.WebUI.Controllers
{
    public class PricingController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public PricingController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Upgrade(string plan, string cycle = "monthly")
        {
            var client = _httpClientFactory.CreateClient("Api");
            var httpContextAccessor = HttpContext.RequestServices.GetRequiredService<IHttpContextAccessor>();
            await Services.HttpClientTokenHelper.AttachBearerTokenAsync(client, httpContextAccessor);

            var res = await client.PostAsync($"/api/Tenants/upgrade-plan?planType={plan}", null);

            if (res.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Planınız başarıyla yükseltildi!";
                return RedirectToAction("Index", "Dashboard");
            }

            TempData["ErrorMessage"] = "Plan yükseltme sırasında bir hata oluştu.";
            return RedirectToAction("Index", "Dashboard");
        }
    }
}
