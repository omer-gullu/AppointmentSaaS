using Appointment_SaaS.WebUI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.IdentityModel.Tokens.Jwt;

namespace Appointment_SaaS.WebUI.Controllers
{
    public class AuthController : Controller
    {
        private readonly HttpClient _httpClient;

        public AuthController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("Api");
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Register(string plan = "trial", string cycle = "monthly")
        {
            var sectors = new List<(int Id, string Name)>();
            try
            {
                var res = await _httpClient.GetAsync("api/Sector");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var arr = JsonDocument.Parse(json).RootElement;
                    if (arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var s in arr.EnumerateArray())
                        {
                            var id = s.TryGetProperty("sectorID", out var sid) ? sid.GetInt32() : 0;
                            var name = s.TryGetProperty("name", out var sname) ? sname.GetString() ?? "" : "";
                            if (id > 0 && !string.IsNullOrEmpty(name))
                                sectors.Add((id, name));
                        }
                    }
                }
            }
            catch { }

            ViewBag.Sectors = sectors;
            ViewBag.SelectedPlan = plan;
            ViewBag.BillingCycle = cycle;
            return View(new RegisterViewModel { SelectedPlan = plan, BillingCycle = cycle });
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            static string ToTitleCase(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                var info = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
                return System.Globalization.CultureInfo.CurrentCulture.TextInfo
                    .ToTitleCase(s.Trim().ToLower(info));
            }

            model.UserFullName = ToTitleCase(model.UserFullName);
            model.BusinessName = ToTitleCase(model.BusinessName);
            model.Address = ToTitleCase(model.Address);
            model.UserEmail = model.UserEmail?.Trim().ToLowerInvariant() ?? string.Empty;

            // ✅ DÜZELTME: planType, billingCycle, identityNumber, birthYear eklendi
            var payload = new
            {
                userFullName = model.UserFullName,
                userEmail = model.UserEmail,
                businessName = model.BusinessName,
                sectorID = model.SectorID,
                phoneNumber = model.PhoneNumber,
                address = model.Address,
                planType = model.SelectedPlan ?? "trial",
                billingCycle = model.BillingCycle ?? "Monthly",
                identityNumber = model.IdentityNumber ?? "00000000000",
                birthYear = model.BirthYear > 0 ? model.BirthYear : 1990,
                cardHolderName = model.CardHolderName,
                cardNumber = model.CardNumber?.Replace(" ", "") ?? string.Empty,
                expireMonth = model.ExpireMonth,
                expireYear = model.ExpireYear,
                cvc = model.Cvc
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync("api/Auth/register-business", content);

            if (res.IsSuccessStatusCode)
            {
                TempData["RegisterSuccess"] = "Kayıt başarıyla tamamlandı! Şimdi WhatsApp numaranız üzerinden giriş yapabilirsiniz.";
                TempData["PrefilledPhone"] = model.PhoneNumber;
                return RedirectToAction("Login", "Auth");
            }

            var err = await res.Content.ReadAsStringAsync();
            string errMsg = err;
            try
            {
                var errJson = JsonDocument.Parse(err);
                errMsg = errJson.RootElement.TryGetProperty("message", out var mp) ? mp.GetString() ?? err : err;
            }
            catch { }

            // Hata durumunda sektörleri yeniden yükle
            var sectors = new List<(int Id, string Name)>();
            try
            {
                var sRes = await _httpClient.GetAsync("api/Sector");
                if (sRes.IsSuccessStatusCode)
                {
                    var sjson = await sRes.Content.ReadAsStringAsync();
                    var sarr = JsonDocument.Parse(sjson).RootElement;
                    if (sarr.ValueKind == JsonValueKind.Array)
                        foreach (var s in sarr.EnumerateArray())
                        {
                            var id = s.TryGetProperty("sectorID", out var sid) ? sid.GetInt32() : 0;
                            var name = s.TryGetProperty("name", out var sname) ? sname.GetString() ?? "" : "";
                            if (id > 0 && !string.IsNullOrEmpty(name)) sectors.Add((id, name));
                        }
                }
            }
            catch { }

            ViewBag.Sectors = sectors;
            ViewBag.SelectedPlan = model.SelectedPlan;
            ViewBag.BillingCycle = model.BillingCycle;
            ViewBag.Error = errMsg;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> GenerateOtp([FromBody] OtpRequestModel request)
        {
            var payload = new { phoneNumber = request.PhoneNumber };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync("api/Auth/generate-otp", content);

            var body = await res.Content.ReadAsStringAsync();
            string message = body;
            try
            {
                var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("message", out var mp))
                    message = mp.GetString() ?? body;
            }
            catch { }

            if (res.IsSuccessStatusCode)
                return Json(new { success = true });

            return StatusCode((int)res.StatusCode, new { success = false, message });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyModel request)
        {
            var payload = new { phoneNumber = request.PhoneNumber, otpCode = request.OtpCode };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync("api/Auth/verify-otp", content);

            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                var tokenData = JsonDocument.Parse(json);
                string token = tokenData.RootElement.GetProperty("token").GetString()!;

                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var claims = jwtToken.Claims.ToList();

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties { IsPersistent = true };

                // ✅ JWT token'ı ASP.NET Core auth cookie'sinin İÇİNE göm
                // Ayrı bir cookie yerine mevcut oturum cookie'si içinde saklanır
                // Bu sayede HTTP/HTTPS farkı, SameSite sorunları tamamen ortadan kalkar
                authProperties.StoreTokens(new[]
                {
                    new Microsoft.AspNetCore.Authentication.AuthenticationToken
                    {
                        Name = "access_token",
                        Value = token
                    }
                });

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return Json(new { success = true });
            }

            var body = await res.Content.ReadAsStringAsync();
            string message = body;
            try
            {
                var errJson = JsonDocument.Parse(body);
                if (errJson.RootElement.TryGetProperty("message", out var mp))
                    message = mp.GetString() ?? body;
            }
            catch { }

            return StatusCode((int)res.StatusCode, new { success = false, message });
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("token"); // JWT cookie'yi de temizle
            return RedirectToAction("Login");
        }
    }

    public class OtpRequestModel
    {
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class OtpVerifyModel
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
    }
}