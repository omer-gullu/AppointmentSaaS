using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Appointment_SaaS.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Appointment_SaaS.WebUI.Services.Abstract;
using Appointment_SaaS.Business.Abstract;
using Microsoft.AspNetCore.Authentication;
using System.Globalization;

namespace Appointment_SaaS.WebUI.Controllers
{
    [Authorize(Roles = "Manager")]
    public class DashboardController : Controller
    {
        private readonly IDashboardApiService _dashboardService;
        private readonly IGoogleCalendarApiService _googleCalendarService;
        private readonly IAppointmentApiService _appointmentService;
        private readonly IEvolutionApiService _evolutionApiService;

        public DashboardController(
            IDashboardApiService dashboardService,
            IGoogleCalendarApiService googleCalendarService,
            IAppointmentApiService appointmentService,
            IEvolutionApiService evolutionApiService)
        {
            _dashboardService = dashboardService;
            _googleCalendarService = googleCalendarService;
            _appointmentService = appointmentService;
            _evolutionApiService = evolutionApiService;
        }

        private int GetCurrentTenantId()
        {
            var tenantIdClaim = User.FindFirst("TenantId")?.Value;
            if (int.TryParse(tenantIdClaim, out int tenantId))
                return tenantId;
            throw new UnauthorizedAccessException("Giriş yapmanız gerekmektedir.");
        }

        private static string ToTurkishTitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            var culture = new CultureInfo("tr-TR");
            return culture.TextInfo.ToTitleCase(value.Trim().ToLower(culture));
        }

        [HttpGet]
        public async Task<IActionResult> GetWhatsAppQr()
        {
            int tenantId = GetCurrentTenantId();
            try
            {
                var viewModel = await _dashboardService.GetDashboardDataAsync(tenantId);
                string? instanceName = viewModel.InstanceName;

                if (string.IsNullOrEmpty(instanceName))
                    return Json(new { success = false, message = "Dükkan bilgisi bulunamadı veya Instance adı atanmamış." });

                await _evolutionApiService.DisconnectInstanceAsync(instanceName);
                await Task.Delay(5000);
                await _evolutionApiService.ConnectInstanceAsync(instanceName);

                var qrCodeBase64 = await _evolutionApiService.GetQrCodeAsync(instanceName);

                if (string.IsNullOrEmpty(qrCodeBase64))
                    return Json(new { success = false, message = "QR kod şu an üretilemedi. Cihaz zaten bağlı olabilir." });

                return Json(new { success = true, qrCode = qrCodeBase64, instanceName });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> Index()
        {
            int tenantId = GetCurrentTenantId();
            var viewModel = await _dashboardService.GetDashboardDataAsync(tenantId);
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAppointment(
            string customerName, string customerPhone, int serviceId, DateTime date, string time, int? appUserId)
        {
            int tenantId = GetCurrentTenantId();
            var startDate = DateTime.Parse($"{date:yyyy-MM-dd}T{time}:00");
            customerName = ToTurkishTitleCase(customerName);

            var (success, msg, newAppointmentId, assignedAppUserId) = await _appointmentService.CreateAppointmentAsync(
                tenantId, customerName, customerPhone, serviceId, startDate, appUserId);

            if (success)
                TempData["Success"] = "Randevu başarıyla eklendi.";
            else
                TempData["Error"] = msg;

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAppointment(
            int appointmentId, string customerName, string customerPhone,
            int serviceId, DateTime date, string time, string? googleEventId, int? appUserId)
        {
            int tenantId = GetCurrentTenantId();
            var startDate = DateTime.Parse($"{date:yyyy-MM-dd}T{time}:00");
            customerName = ToTurkishTitleCase(customerName);

            var (success, msg) = await _appointmentService.UpdateAppointmentAsync(
                tenantId, appointmentId, customerName, customerPhone, serviceId, startDate, googleEventId, appUserId);

            if (success)
                TempData["Success"] = "Randevu başarıyla güncellendi.";
            else
                TempData["Error"] = msg;

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAppointment(int appointmentId, string? googleEventId, int? appUserId)
        {
            var (success, msg) = await _appointmentService.DeleteAppointmentAsync(appointmentId);

            TempData[success ? "Success" : "Error"] = msg;
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CreateService(string name, decimal price, int durationMinutes)
        {
            int tenantId = GetCurrentTenantId();
            try 
            {
                var httpClient = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("Api");
                await Services.HttpClientTokenHelper.AttachBearerTokenAsync(httpClient, HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>());
                
                var payload = new { name, price, durationMinutes, tenantID = tenantId };
                var content = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync("api/Services", content);
                
                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Hizmet başarıyla eklendi.";
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"API Hatası ({(int)response.StatusCode}): {body}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Sistem Hatası: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateService(int serviceId, string name, decimal price, int durationMinutes)
        {
            int tenantId = GetCurrentTenantId();
            bool success = await _dashboardService.UpdateServiceAsync(tenantId, serviceId, name, price, durationMinutes);

            TempData[success ? "Success" : "Error"] = success
                ? "Hizmet başarıyla güncellendi."
                : "Hata: Hizmet güncellenemedi.";

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteService(int serviceId)
        {
            bool success = await _dashboardService.DeleteServiceAsync(serviceId);

            TempData[success ? "Success" : "Error"] = success
                ? "Hizmet başarıyla silindi."
                : "Hata: Hizmet silinemedi.";

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAssistant(bool isActive)
        {
            int tenantId = GetCurrentTenantId();
            bool success = await _dashboardService.ToggleAssistantAsync(tenantId, isActive);

            if (!success)
                return Json(new { success = false, message = "AI durumu güncellenemedi veya WhatsApp bağlantısı değiştirilemedi." });

            var viewModel = await _dashboardService.GetDashboardDataAsync(tenantId);
            return Json(new
            {
                success = true,
                isActive,
                isWhatsAppConnected = viewModel.IsWhatsAppConnected
            });
        }

        [HttpGet]
        public async Task<IActionResult> CheckWhatsAppStatus()
        {
            try
            {
                int tenantId = GetCurrentTenantId();
                var viewModel = await _dashboardService.GetDashboardDataAsync(tenantId);
                
                if (string.IsNullOrWhiteSpace(viewModel.InstanceName))
                    return Json(new { connected = false });

                return Json(new { connected = viewModel.IsWhatsAppConnected });
            }
            catch
            {
                return Json(new { connected = false });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelSubscription()
        {
            int tenantId = GetCurrentTenantId();
            var (success, msg) = await _dashboardService.CancelSubscriptionAsync(tenantId);

            TempData[success ? "Success" : "Error"] = msg;
            return RedirectToAction("Index");
        }

        public IActionResult ConnectGoogle()
        {
            var authUrl = _googleCalendarService.GetConnectUrl();
            return Redirect(authUrl);
        }

        public IActionResult ConnectStaffGoogle(int staffId)
        {
            var authUrl = _googleCalendarService.GetConnectStaffUrl(staffId);
            return Redirect(authUrl);
        }

        [HttpGet]
        public async Task<IActionResult> GoogleCallback(string code, string error, string state = null)
        {
            if (!string.IsNullOrEmpty(error))
            {
                TempData["Error"] = $"Google erişim hatası: {error}";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrEmpty(code))
            {
                TempData["Error"] = "Google'dan yetkilendirme kodu alınamadı.";
                return RedirectToAction("Index");
            }

            int tenantId = GetCurrentTenantId();
            bool success;
            string msg;

            if (!string.IsNullOrEmpty(state) && state.StartsWith("staff_"))
            {
                if (int.TryParse(state.Replace("staff_", ""), out int staffId))
                {
                    (success, msg) = await _googleCalendarService.ProcessStaffCallbackAsync(code, tenantId, staffId);
                }
                else
                {
                    success = false;
                    msg = "Geçersiz personel kimliği.";
                }
            }
            else
            {
                (success, msg) = await _googleCalendarService.ProcessCallbackAsync(code, tenantId);
            }

            if (success)
                TempData["Success"] = "Google Hesabınız başarıyla bağlandı!";
            else
                TempData["Error"] = $"Google bilgileri kaydedilemedi: {msg}";

            return RedirectToAction("Index");
        }

        /// <summary>
        /// DEBUG: Token ve API bağlantısını test eder. Production'da kaldırılmalı.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DebugTokenTest()
        {
            var httpContext = HttpContext;
            var tokenFromAuth = await httpContext.GetTokenAsync("access_token");
            var tokenFromCookie = httpContext.Request.Cookies["token"];
            var token = tokenFromAuth ?? tokenFromCookie;
            var tenantId = User.FindFirst("TenantId")?.Value ?? "YOK";

            // Gerçek API çağrısı testi
            string apiTestResult = "Yapılmadı";
            string apiTestHeaders = "";
            string apiBaseUrl = "";
            try
            {
                var factory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                var testClient = factory.CreateClient("Api");
                apiBaseUrl = testClient.BaseAddress?.ToString() ?? "BOŞ";
                
                if (!string.IsNullOrEmpty(token))
                {
                    testClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
                
                apiTestHeaders = string.Join("; ", testClient.DefaultRequestHeaders
                    .Select(h => $"{h.Key}={string.Join(",", h.Value)}"));

                // AllowAnonymous endpoint'i test et
                var anonResponse = await testClient.GetAsync($"api/Tenants/{tenantId}");
                string anonResult = $"GET Tenants/{tenantId} (AllowAnonymous) → {(int)anonResponse.StatusCode} {anonResponse.StatusCode}";
                
                // Korumalı endpoint test et  
                var authResponse = await testClient.PostAsync(
                    $"api/Tenants/UpdateBotStatus?tenantId={tenantId}&isBotActive=true", null);
                string authResult = $"POST UpdateBotStatus (Authorize) → {(int)authResponse.StatusCode} {authResponse.StatusCode}";
                string authBody = await authResponse.Content.ReadAsStringAsync();

                apiTestResult = $"{anonResult} | {authResult} | Body: {authBody}";
            }
            catch (Exception ex)
            {
                apiTestResult = $"HATA: {ex.Message}";
            }

            var debugInfo = new
            {
                HasAuthToken = !string.IsNullOrEmpty(tokenFromAuth),
                AuthTokenLength = tokenFromAuth?.Length ?? 0,
                AuthTokenPreview = tokenFromAuth != null ? tokenFromAuth[..Math.Min(20, tokenFromAuth.Length)] + "..." : "NULL",
                HasCookieToken = !string.IsNullOrEmpty(tokenFromCookie),
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                UserRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "YOK",
                TenantId = tenantId,
                ApiBaseUrl = apiBaseUrl,
                ApiHeaders = apiTestHeaders,
                ApiTestResult = apiTestResult
            };

            return Json(debugInfo);
        }

        public IActionResult ConnectWhatsApp()
        {
            return RedirectToAction("Index", "Instance");
        }

        /// <summary>
        /// Personel ekleme — JavaScript'ten gelen isteği Backend API'ye proxy eder.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddStaff([FromBody] AddStaffRequest request)
        {
            try
            {
                int tenantId = GetCurrentTenantId();
                var factory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient("Api");
                await Services.HttpClientTokenHelper.AttachBearerTokenAsync(
                    httpClient, HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>());

                var payload = new
                {
                    firstName = request.FirstName,
                    lastName = request.LastName,
                    email = request.Email,
                    phoneNumber = request.PhoneNumber,
                    specialization = request.Specialization,
                    googleCalendarId = request.GoogleCalendarId
                };

                var content = new System.Net.Http.StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("api/AppUsers/add-staff", content);
                var body = await response.Content.ReadAsStringAsync();

                object? jsonBody = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(body))
                        jsonBody = System.Text.Json.JsonSerializer.Deserialize<object>(body);
                }
                catch { /* API'den JSON olmayan yanıt geldiyse yoksay */ }

                if (response.IsSuccessStatusCode)
                    return Ok(jsonBody ?? new { message = "Personel başarıyla eklendi." });

                // 403 plan limiti gibi özel durumları olduğu gibi ilet
                return StatusCode((int)response.StatusCode,
                    jsonBody ?? new { message = body ?? "Bilinmeyen hata." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Sistem hatası: {ex.Message}" });
            }
        }

        /// <summary>
        /// Personel silme — JavaScript'ten gelen isteği Backend API'ye proxy eder.
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> RemoveStaff(int userId)
        {
            try
            {
                var factory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient("Api");
                await Services.HttpClientTokenHelper.AttachBearerTokenAsync(
                    httpClient, HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>());

                var response = await httpClient.DeleteAsync($"api/AppUsers/{userId}");
                var body = await response.Content.ReadAsStringAsync();

                object? jsonBody = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(body))
                        jsonBody = System.Text.Json.JsonSerializer.Deserialize<object>(body);
                }
                catch { }

                if (response.IsSuccessStatusCode)
                    return Ok(jsonBody ?? new { message = "Personel başarıyla eklendi." });

                // 403 plan limiti gibi özel durumları olduğu gibi ilet
                return StatusCode((int)response.StatusCode,
                    jsonBody ?? new { message = $"API Hatası ({(int)response.StatusCode}): {body}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Sistem hatası: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBusinessHours([FromBody] List<Appointment_SaaS.Core.DTOs.BusinessHourDto> hours)
        {
            try
            {
                int tenantId = GetCurrentTenantId();
                var factory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient("Api");
                await Services.HttpClientTokenHelper.AttachBearerTokenAsync(
                    httpClient, HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>());

                var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(hours), System.Text.Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync($"api/Tenants/update-hours", content);

                var body = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode) return Ok(new { success = true, message = "Çalışma saatleri başarıyla güncellendi." });
                return StatusCode((int)response.StatusCode, new { message = body });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
        [HttpPost]
        public async Task<IActionResult> SendFeedback(string feedbackType, string message)
        {
            try
            {
                int tenantId = GetCurrentTenantId();
                var factory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient("Api");
                await Services.HttpClientTokenHelper.AttachBearerTokenAsync(
                    httpClient, HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>());

                var payload = new
                {
                    tenantId,
                    feedbackType,
                    message,
                    sentAt = DateTime.UtcNow
                };

                var content = new System.Net.Http.StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("api/Feedbacks", content);

                if (response.IsSuccessStatusCode)
                    TempData["Success"] = "Mesajınız iletildi. Teşekkürler! 🙏";
                else
                    TempData["Error"] = "Mesaj gönderilemedi, lütfen tekrar deneyin.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Sistem hatası: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// Personel ekleme isteği için DTO
    /// </summary>
    public class AddStaffRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Specialization { get; set; }
        public string? GoogleCalendarId { get; set; }
    }
}