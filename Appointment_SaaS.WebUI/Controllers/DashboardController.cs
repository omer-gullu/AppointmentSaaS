using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Appointment_SaaS.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Appointment_SaaS.WebUI.Services.Abstract;
using Appointment_SaaS.Business.Abstract;
using Microsoft.AspNetCore.Authentication;
using System.Globalization;
using Appointment_SaaS.Core.DTOs;

namespace Appointment_SaaS.WebUI.Controllers
{
    [Authorize(Roles = "Manager")]
    public class DashboardController : Controller
    {
        private readonly IDashboardApiService _dashboardService;
        private readonly IGoogleCalendarApiService _googleCalendarService;
        private readonly IAppointmentApiService _appointmentService;
        private readonly IEvolutionApiService _evolutionApiService;
        private readonly ITenantService _tenantService;
        private readonly IWhatsAppBlockedPhoneApiService _blockedPhoneApiService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            IDashboardApiService dashboardService,
            IGoogleCalendarApiService googleCalendarService,
            IAppointmentApiService appointmentService,
            IEvolutionApiService evolutionApiService,
            ITenantService tenantService,
            IWhatsAppBlockedPhoneApiService blockedPhoneApiService,
            ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _googleCalendarService = googleCalendarService;
            _appointmentService = appointmentService;
            _evolutionApiService = evolutionApiService;
            _tenantService = tenantService;
            _blockedPhoneApiService = blockedPhoneApiService;
            _logger = logger;
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

                // ── OTOMATİK DÜZELTME: Türkçe karakter kontrolü ──
                var turkishChars = "çÇğĞıİöÖşŞüÜ";
                if (instanceName.Any(c => turkishChars.Contains(c)))
                {
                    _logger.LogInformation("[AutoFix] Instance isminde geçersiz karakter: {OldName}", instanceName);
                    var mapping = new Dictionary<char, char> {
                        {'ç','c'}, {'Ç','C'}, {'ğ','g'}, {'Ğ','G'}, {'ı','i'}, {'İ','I'},
                        {'ö','o'}, {'Ö','O'}, {'ş','s'}, {'Ş','S'}, {'ü','u'}, {'Ü','U'}
                    };
                    var normalized = instanceName;
                    foreach (var m in mapping) normalized = normalized.Replace(m.Key, m.Value);

                    var tenant = await _tenantService.GetByIdAsync(tenantId);
                    if (tenant != null)
                    {
                        tenant.InstanceName = normalized;
                        await _tenantService.UpdateAsync(tenant);
                        await _evolutionApiService.CreateInstanceAsync(normalized);
                        instanceName = normalized;
                        _logger.LogInformation("[AutoFix] Instance ismi düzeltildi: {NewName}", instanceName);
                    }
                }

                // ── QR AKIŞI: Önce bağlantıyı kes, sonra TEK çağrıyla bağlan + QR al ──
                // Eski akış: Disconnect → Connect (QR kaybolur) → GetQr = hep null
                // Yeni akış: Disconnect → ConnectAndGetQr (tek çağrı, QR kaybolmaz)
                await _evolutionApiService.DisconnectInstanceAsync(instanceName);
                await Task.Delay(2000);

                var qrCodeBase64 = await _evolutionApiService.ConnectAndGetQrAsync(instanceName);

                if (string.IsNullOrEmpty(qrCodeBase64))
                    return Json(new { success = false, message = "QR kod üretilemedi. Lütfen birkaç saniye bekleyip tekrar deneyin." });

                return Json(new { success = true, qrCode = qrCodeBase64, instanceName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Dashboard] GetWhatsAppQr exception. TenantId={TenantId}", tenantId);
                return Json(new { success = false, message = "Beklenmeyen bir hata oluştu." });
            }
        }

        public async Task<IActionResult> Index()
        {
            int tenantId = GetCurrentTenantId();
            var viewModel = await _dashboardService.GetDashboardDataAsync(tenantId);
            return View(viewModel);
        }

        [HttpPost]
[ValidateAntiForgeryToken]
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
[ValidateAntiForgeryToken]
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
[ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAppointment(int appointmentId, string? googleEventId, int? appUserId)
        {
            var (success, msg) = await _appointmentService.DeleteAppointmentAsync(appointmentId);

            TempData[success ? "Success" : "Error"] = msg;
            return RedirectToAction("Index");
        }

        [HttpPost]
[ValidateAntiForgeryToken]
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

            return RedirectToAction("Services", "BusinessSettings");
        }

        [HttpPost]
[ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateService(int serviceId, string name, decimal price, int durationMinutes)
        {
            int tenantId = GetCurrentTenantId();
            bool success = await _dashboardService.UpdateServiceAsync(tenantId, serviceId, name, price, durationMinutes);

            TempData[success ? "Success" : "Error"] = success
                ? "Hizmet başarıyla güncellendi."
                : "Hata: Hizmet güncellenemedi.";

            return RedirectToAction("Services", "BusinessSettings");
        }

        [HttpPost]
[ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteService(int serviceId)
        {
            bool success = await _dashboardService.DeleteServiceAsync(serviceId);

            TempData[success ? "Success" : "Error"] = success
                ? "Hizmet başarıyla silindi."
                : "Hata: Hizmet silinemedi.";

            return RedirectToAction("Services", "BusinessSettings");
        }

        [HttpPost]
[ValidateAntiForgeryToken]
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
[ValidateAntiForgeryToken]
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

        /// <summary>
        /// Personelin kayıtlı Google refresh token'ı ile access token alınabiliyor mu test eder.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RefreshStaffGoogle(int staffId)
        {
            if (staffId <= 0)
                return BadRequest(new { success = false, message = "Geçersiz personel." });

            try
            {
                var factory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient("Api");
                await Services.HttpClientTokenHelper.AttachBearerTokenAsync(
                    httpClient,
                    HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>());

                var response = await httpClient.GetAsync($"api/AppUsers/{staffId}/google-token");
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Google Takvim bağlantısı aktif; token yenilendi."
                    });
                }

                var needsReconnect = body.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase)
                    || body.Contains("bağlı değil", StringComparison.OrdinalIgnoreCase);

                string message = needsReconnect
                    ? "Google bağlantısı geçersiz veya süresi dolmuş. Google ile yeniden yetkilendirmeniz gerekiyor."
                    : "Google token yenilenemedi. Lütfen tekrar deneyin.";

                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("error", out var err))
                        message = err.GetString() ?? message;
                    if (doc.RootElement.TryGetProperty("detail", out var detail)
                        && detail.GetString()?.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase) == true)
                        needsReconnect = true;
                }
                catch { /* API gövdesi JSON değilse varsayılan mesaj */ }

                return StatusCode((int)response.StatusCode, new { success = false, message, needsReconnect });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshStaffGoogle hatası. StaffId={StaffId}", staffId);
                return StatusCode(500, new { success = false, message = "Sistem hatası. Lütfen tekrar deneyin." });
            }
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

        public IActionResult ConnectWhatsApp()
        {
            return RedirectToAction("Index", "Instance");
        }


        /// <summary>
        /// Personel ekleme — JavaScript'ten gelen isteği Backend API'ye proxy eder.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
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
                    return Ok(jsonBody ?? new { message = "Personel başarıyla silindi." });

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBreakTime([FromBody] Appointment_SaaS.Core.DTOs.BreakTimeSettingsDto settings)
        {
            try
            {
                var factory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient("Api");
                await Services.HttpClientTokenHelper.AttachBearerTokenAsync(
                    httpClient, HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>());

                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(settings),
                    System.Text.Encoding.UTF8,
                    "application/json");
                var response = await httpClient.PostAsync("api/Tenants/update-break-time", content);

                var body = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return Ok(new { success = true, message = "Mola saatleri başarıyla güncellendi." });

                string message = body;
                try
                {
                    var json = System.Text.Json.JsonDocument.Parse(body);
                    if (json.RootElement.TryGetProperty("message", out var mp))
                        message = mp.GetString() ?? body;
                    else if (json.RootElement.TryGetProperty("Message", out var mp2))
                        message = mp2.GetString() ?? body;
                }
                catch { }

                if (string.IsNullOrWhiteSpace(message))
                    message = $"API hatası ({(int)response.StatusCode})";

                return StatusCode((int)response.StatusCode, new { success = false, message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
[ValidateAntiForgeryToken]
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
[ValidateAntiForgeryToken]
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

        public async Task<IActionResult> GetHolidays()
        {
            var tenantId = GetCurrentTenantId();
            var result = await _tenantService.GetHolidaysAsync(tenantId);
            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> SeedDefaultHolidays()
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                var added = await _tenantService.SeedDefaultHolidaysIfEmptyAsync(tenantId);
                if (added == 0)
                    return Json(new { success = true, message = "Zaten tatil kaydı var veya öneriler yüklenemedi.", added });
                return Json(new { success = true, message = $"{added} resmi tatil önerisi eklendi.", added });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> AddHoliday([FromBody] HolidayCreateDto dto)
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                await _tenantService.AddHolidayAsync(tenantId, dto.Date, dto.Name);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> DeleteHoliday(int id)
        {
            try
            {
                var tenantId = GetCurrentTenantId();
                await _tenantService.DeleteHolidayAsync(tenantId, id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult BlockedPhones()
        {
            return RedirectToAction("BlockedPhones", "BusinessSettings");
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