using Appointment_SaaS.Core.Utilities;
using Appointment_SaaS.WebUI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

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
[ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            model.UserFullName = TurkishTextNormalizer.ToTurkishTitleCase(model.UserFullName);
            model.BusinessName = TurkishTextNormalizer.ToTurkishTitleCase(model.BusinessName);
            model.Address = TurkishTextNormalizer.ToTurkishTitleCase(model.Address);
            model.UserEmail = model.UserEmail?.Trim().ToLowerInvariant() ?? string.Empty;
            model.IdentityNumber = TurkishIdentityValidator.NormalizeIdentityNumber(model.IdentityNumber);

            var paymentCallbackAbsolute = Url.Action("PaymentCallback", "Auth", values: null, protocol: Request.Scheme, host: Request.Host.Value)
                ?? $"{Request.Scheme}://{Request.Host}/Auth/PaymentCallback";

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
                identityNumber = model.IdentityNumber,
                birthYear = model.BirthYear,
                paymentCallbackUrl = paymentCallbackAbsolute
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage res;
            try
            {
                res = await _httpClient.PostAsync("api/Auth/register-business", content);
            }
            catch (TaskCanceledException)
            {
                ViewBag.Error =
                    "Kayıt işlemi zaman aşımına uğradı. İşletme kaydı oluşmuş olabilir; aynı e-posta/telefon ile tekrar denemeyin. Destek ile iletişime geçin veya giriş yapmayı deneyin.";
                return await ReloadRegisterViewAsync(model);
            }

            if (res.IsSuccessStatusCode)
            {
                var responseContent = await res.Content.ReadAsStringAsync();
                var resultJson = JsonDocument.Parse(responseContent).RootElement;

                var tenantId = resultJson.TryGetProperty("tenantId", out var tidProp)
                    ? tidProp.GetInt32()
                    : resultJson.TryGetProperty("TenantId", out var tidProp2)
                        ? tidProp2.GetInt32()
                        : 0;

                StoreSetupSession(tenantId, model.PhoneNumber, model.UserEmail, model.UserFullName);

                string? checkoutFormScript = resultJson.TryGetProperty("checkoutFormContent", out var scriptProp)
                    ? scriptProp.GetString()
                    : null;

                var paymentSkipped = resultJson.TryGetProperty("paymentCheckoutSkipped", out var skippedProp)
                    && skippedProp.ValueKind == JsonValueKind.True;
                var skipReason = resultJson.TryGetProperty("paymentCheckoutSkipReason", out var reasonProp)
                                 && reasonProp.ValueKind == JsonValueKind.String
                    ? reasonProp.GetString()
                    : null;

                if (!string.IsNullOrWhiteSpace(checkoutFormScript))
                {
                    ViewBag.CheckoutFormScript = checkoutFormScript;
                    return View(model);
                }

                if (paymentSkipped && !string.IsNullOrWhiteSpace(skipReason))
                {
                    if (tenantId > 0)
                    {
                        TempData["RegisterSuccess"] =
                            "Kayıt tamamlandı. Personel listeniz boş; OTP ile giriş için önce «İlk personeli ekle» adımını tamamlayın.";
                        return RedirectToAction(nameof(Login));
                    }

                    ViewBag.PaymentSkippedInfo = skipReason;
                    var skippedSectors = new List<(int Id, string Name)>();
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
                                    if (id > 0 && !string.IsNullOrEmpty(name)) skippedSectors.Add((id, name));
                                }
                        }
                    }
                    catch { }

                    ViewBag.Sectors = skippedSectors;
                    ViewBag.SelectedPlan = model.SelectedPlan;
                    ViewBag.BillingCycle = model.BillingCycle;
                    return View(model);
                }

                TempData["RegisterSuccess"] =
                    "Ödeme alındı. Personel listeniz boş; OTP ile giriş için önce «İlk personeli ekle» adımını tamamlayın.";
                return RedirectToAction(nameof(Login));
            }

            var err = await res.Content.ReadAsStringAsync();
            string errMsg = err;
            try
            {
                var errJson = JsonDocument.Parse(err);
                errMsg = errJson.RootElement.TryGetProperty("message", out var mp) ? mp.GetString() ?? err : err;
            }
            catch { }

            ViewBag.Error = errMsg;
            return await ReloadRegisterViewAsync(model);
        }

        private async Task<IActionResult> ReloadRegisterViewAsync(RegisterViewModel model)
        {
            var reloadSectors = new List<(int Id, string Name)>();
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
                            if (id > 0 && !string.IsNullOrEmpty(name)) reloadSectors.Add((id, name));
                        }
                }
            }
            catch { }

            ViewBag.Sectors = reloadSectors;
            ViewBag.SelectedPlan = model.SelectedPlan;
            ViewBag.BillingCycle = model.BillingCycle;
            return View(model);
        }

        [HttpPost]
[ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateOtp([FromBody] OtpRequestModel request)
        {
            request.PhoneNumber = OtpPhoneNormalizer.Normalize(request.PhoneNumber);
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
[ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyModel request)
        {
            try
            {
                request.PhoneNumber = OtpPhoneNormalizer.Normalize(request.PhoneNumber);
                var otpDigits = new string((request.OtpCode ?? "").Where(char.IsDigit).ToArray());
                if (otpDigits.Length >= 6) otpDigits = otpDigits[^6..];
                request.OtpCode = otpDigits.PadLeft(6, '0');

                var payload = new { phoneNumber = request.PhoneNumber, otpCode = request.OtpCode };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync("api/Auth/verify-otp", content);

                if (!res.IsSuccessStatusCode)
                {
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

                var json = await res.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement;
                if (!root.TryGetProperty("token", out var tokenProp) || tokenProp.ValueKind != JsonValueKind.String)
                {
                    return StatusCode(500, new { success = false, message = "Oturum oluşturulamadı. Lütfen tekrar deneyin." });
                }

                var token = tokenProp.GetString();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return StatusCode(500, new { success = false, message = "Oturum oluşturulamadı. Lütfen tekrar deneyin." });
                }

                var claims = BuildClaimsFromAccessToken(token);
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties { IsPersistent = true };
                authProperties.StoreTokens(new[]
                {
                    new AuthenticationToken { Name = "access_token", Value = token }
                });

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return Json(new { success = true });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "Giriş tamamlanamadı. Lütfen tekrar deneyin." });
            }
        }

        private static List<Claim> BuildClaimsFromAccessToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var claims = new List<Claim>();

            string? Find(params string[] types)
            {
                foreach (var type in types)
                {
                    var value = jwt.Claims.FirstOrDefault(c =>
                        string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
                return null;
            }

            var nameId = Find(ClaimTypes.NameIdentifier, "sub", "nameid");
            if (!string.IsNullOrWhiteSpace(nameId))
                claims.Add(new Claim(ClaimTypes.NameIdentifier, nameId));

            var email = Find(ClaimTypes.Email, "email");
            if (!string.IsNullOrWhiteSpace(email))
                claims.Add(new Claim(ClaimTypes.Email, email));

            var name = Find(ClaimTypes.Name, "unique_name", "name");
            if (!string.IsNullOrWhiteSpace(name))
                claims.Add(new Claim(ClaimTypes.Name, name));

            var tenantId = Find("TenantId", "tenantId");
            if (!string.IsNullOrWhiteSpace(tenantId))
                claims.Add(new Claim("TenantId", tenantId));

            var securityStamp = Find("SecurityStamp", "securityStamp");
            if (!string.IsNullOrWhiteSpace(securityStamp))
                claims.Add(new Claim("SecurityStamp", securityStamp));

            foreach (var role in jwt.Claims.Where(c =>
                         string.Equals(c.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                         || string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(role.Value))
                    claims.Add(new Claim(ClaimTypes.Role, role.Value));
            }

            if (claims.Count == 0)
                throw new InvalidOperationException("JWT içinde geçerli kullanıcı bilgisi bulunamadı.");

            return claims;
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("token"); // JWT cookie'yi de temizle
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult SetupFirstStaff()
        {
            var model = LoadSetupSession();
            if (model.TenantId <= 0)
            {
                TempData["RegisterError"] = "Kurulum oturumu bulunamadı. Lütfen kayıt akışını tekrarlayın veya giriş deneyin.";
                return RedirectToAction(nameof(Login));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetupFirstStaff(SetupFirstStaffViewModel model)
        {
            if (model.TenantId <= 0)
            {
                TempData["RegisterError"] = "Geçersiz işletme. Kaydı tekrarlayın.";
                return RedirectToAction(nameof(Login));
            }

            var nameParts = (model.UserFullName ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstName = nameParts.Length > 1
                ? string.Join(' ', nameParts.Take(nameParts.Length - 1))
                : nameParts.FirstOrDefault() ?? "";
            var lastName = nameParts.Length > 1 ? nameParts[^1] : "";

            var payload = new
            {
                tenantId = model.TenantId,
                firstName,
                lastName,
                email = model.UserEmail?.Trim().ToLowerInvariant() ?? "",
                phoneNumber = model.PhoneNumber,
                specialization = model.Specialization
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync("api/AppUsers/bootstrap-first-staff", content);
            var body = await res.Content.ReadAsStringAsync();

            if (res.IsSuccessStatusCode)
            {
                ClearSetupSession();
                TempData["RegisterSuccess"] =
                    "Personel kaydedildi. OTP ile giriş yapın; ardından Personel sayfasından Google Takvim bağlayın.";
                TempData["PrefilledPhone"] = model.PhoneNumber;
                return RedirectToAction(nameof(Login));
            }

            string errMsg = body;
            try
            {
                var errJson = JsonDocument.Parse(body);
                errMsg = errJson.RootElement.TryGetProperty("message", out var mp)
                    ? mp.GetString() ?? body
                    : errJson.RootElement.TryGetProperty("Message", out var mp2)
                        ? mp2.GetString() ?? body
                        : body;
            }
            catch { }

            model.ErrorMessage = errMsg;
            return View(model);
        }

        private void StoreSetupSession(int tenantId, string phone, string email, string fullName)
        {
            TempData["SetupTenantId"] = tenantId.ToString();
            TempData["SetupPhone"] = phone;
            TempData["SetupEmail"] = email;
            TempData["SetupFullName"] = fullName;
        }

        private SetupFirstStaffViewModel LoadSetupSession()
        {
            int.TryParse(TempData["SetupTenantId"]?.ToString(), out var tenantId);
            var phone = TempData["SetupPhone"]?.ToString() ?? "";
            var email = TempData["SetupEmail"]?.ToString() ?? "";
            var fullName = TempData["SetupFullName"]?.ToString() ?? "";

            // Ödeme dönüşünde form tekrar gösterilsin
            if (tenantId > 0)
                StoreSetupSession(tenantId, phone, email, fullName);

            return new SetupFirstStaffViewModel
            {
                TenantId = tenantId,
                PhoneNumber = phone
            };
        }

        private void ClearSetupSession()
        {
            TempData.Remove("SetupTenantId");
            TempData.Remove("SetupPhone");
            TempData.Remove("SetupEmail");
            TempData.Remove("SetupFullName");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> PaymentCallback([FromForm] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["RegisterError"] = "Ödeme işlemi başarısız veya iptal edildi.";
                return RedirectToAction("Register");
            }

            var payload = new { token = token };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync("api/Auth/payment-callback", content);

            if (res.IsSuccessStatusCode)
            {
                if (User.Identity?.IsAuthenticated == true)
                {
                    TempData["Success"] = "Ödemeniz alındı. Planınız ve abonelik bitiş tarihi güncellendi.";
                    return RedirectToAction("Index", "Dashboard");
                }

                TempData["RegisterSuccess"] =
                    "Ödemeniz alındı. Personel listeniz boş; OTP ile giriş için önce «İlk personeli ekle» adımını tamamlayın.";
                return RedirectToAction(nameof(Login));
            }
            else
            {
                TempData["RegisterError"] = "Ödeme işlemi onaylanamadı. Lütfen destek ekibi ile iletişime geçiniz.";
                return RedirectToAction("Register");
            }
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