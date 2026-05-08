using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace Appointment_SaaS.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IIyzicoPaymentService _iyzicoPaymentService;
    private readonly IAppUserService _appUserService;

    public TenantsController(
        ITenantService tenantService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IIyzicoPaymentService iyzicoPaymentService,
        IAppUserService appUserService)
    {
        _tenantService = tenantService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _iyzicoPaymentService = iyzicoPaymentService;
        _appUserService = appUserService;
    }

    // ─── Yardımcı: JWT'den aktif kullanıcının TenantId'sini okur ────────────
    private int? GetCurrentTenantId()
    {
        var claim = User.FindFirst("TenantId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private bool IsAdmin() => User.IsInRole("Admin");

    // ─── Listeleme ────────────────────────────────────────────────────────────

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll() => Ok(await _tenantService.GetAllAsync());

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id)
    {
        var tenant = await _tenantService.GetByIdAsync(id);
        if (tenant == null) return NotFound();
        return Ok(tenant);
    }

    // ─── Oluşturma (Sadece Admin) ─────────────────────────────────────────────

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] TenantCreateDto dto)
    {
        try
        {
            var id = await _tenantService.AddTenantAsync(dto, string.Empty);
            return Ok(new { Status = "Başarılı", Id = id });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    // ─── Güncelleme — Ownership kontrolü ─────────────────────────────────────

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TenantUpdateDto dto)
    {
        var tenant = await _tenantService.GetByIdAsync(id);
        if (tenant == null) return NotFound(new { Message = "İşletme bulunamadı." });

        // Sadece Admin veya bu tenant'ın sahibi güncelleyebilir
        if (!IsAdmin() && GetCurrentTenantId() != id)
            return StatusCode(403, new { Message = "Bu işletmeyi güncelleme yetkiniz yok." });

        tenant.Name = dto.Name ?? tenant.Name;
        tenant.PhoneNumber = dto.PhoneNumber ?? tenant.PhoneNumber;
        tenant.Address = dto.Address ?? tenant.Address;
        tenant.InstanceName = dto.InstanceName ?? tenant.InstanceName;
        tenant.IsActive = dto.IsActive;

        // IsTrial ve SubscriptionEndDate yalnızca Admin tarafından değiştirilebilir
        if (IsAdmin())
        {
            tenant.IsTrial = dto.IsTrial;
            tenant.SubscriptionEndDate = dto.SubscriptionEndDate;
        }

        await _tenantService.UpdateAsync(tenant);
        return Ok(new { Status = "Güncellendi" });
    }

    // ─── Silme — Admin veya tenant'ın kendisi ────────────────────────────────

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenant = await _tenantService.GetByIdAsync(id);
        if (tenant == null) return NotFound(new { Message = "İşletme bulunamadı." });

        // Sadece Admin veya bu tenant'ın sahibi silebilir
        if (!IsAdmin() && GetCurrentTenantId() != id)
            return StatusCode(403, new { Message = "Bu işletmeyi silme yetkiniz yok." });

        await _tenantService.DeleteAsync(tenant);
        return Ok(new { Status = "Silindi" });
    }

    // ─── Çalışma Saatleri Güncelleme ─────────────────────────────────────────
    [HttpPost("update-hours")]
    public async Task<IActionResult> UpdateBusinessHours([FromBody] List<Appointment_SaaS.Core.DTOs.BusinessHourDto> hours)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return Unauthorized(new { Message = "Geçersiz oturum." });

        try
        {
            await _tenantService.UpdateBusinessHoursAsync(tenantId.Value, hours);
            return Ok(new { Message = "Çalışma saatleri başarıyla güncellendi." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Güncelleme sırasında hata oluştu: " + ex.Message });
        }
    }

    [HttpPost("upgrade-plan")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> UpgradePlan([FromQuery] string planType)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return Unauthorized(new { Message = "Yetkisiz erişim." });

        var tenant = await _tenantService.GetByIdAsync(tenantId.Value);
        if (tenant == null) return NotFound(new { Message = "İşletme bulunamadı." });

        tenant.PlanType = planType;
        await _tenantService.UpdateAsync(tenant);

        return Ok(new { success = true, Message = "Plan başarıyla güncellendi." });
    }

    // ─── n8n Mega Context — GoogleToken DÖNMEZ ───────────────────────────────
    /// <summary>
    /// n8n workflow başında çağrılır. İşletmenin bugünkü randevularını ve
    /// yapılandırmasını döndürür. GoogleToken bu endpoint'ten DÖNDÜRÜLMEZ;
    /// token için ayrı GetGoogleAccessToken endpoint'i kullanılır.
    /// </summary>
    [HttpGet("GetContextByInstance")]
    [AllowAnonymous]
    public async Task<IActionResult> GetContextByInstance([FromQuery] string instanceName)
    {
        var tenant = await _tenantService.GetContextByInstanceAsync(instanceName);
        if (tenant == null) return NotFound(new { Message = "İşletme bulunamadı." });

        if (!tenant.IsActive || !tenant.IsSubscriptionActive)
            return StatusCode(403, new { Message = "Bu işletme şu an hizmet veremiyor." });
        if (tenant.IsBlacklisted)
            return StatusCode(403, new { Message = "Bu hesaba erişim engellendi." });

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var todayAppointments = tenant.Appointments?
            .Where(a => a.StartDate >= today && a.StartDate < tomorrow)
            .OrderBy(a => a.StartDate)
            .Select(a => new
            {
                Id = a.AppointmentID,
                CustomerName = a.CustomerName,
                CustomerPhone = a.CustomerPhone,
                Start = a.StartDate.ToString("HH:mm"),
                End = a.EndDate.ToString("HH:mm"),
                Status = a.Status,
                ServiceId = a.ServiceID,
                GoogleEventId = a.GoogleEventID
            }).ToList();

        var megaContext = new
        {
            ShopName = tenant.Name,
            Address = tenant.Address,
            Phone = tenant.PhoneNumber,
            GoogleEmail = tenant.GoogleEmail,
            TenantID = tenant.TenantID,
            // GoogleToken burada YOK — /GetGoogleAccessToken endpoint'i kullanılmalı
            Services = tenant.Services?.Select(s => new
            {
                Id = s.ServiceID,
                Name = s.Name,
                DurationInMinutes = s.DurationInMinutes,
                Price = s.Price
            }),
            BusinessHours = tenant.BusinessHours?.Select(b => new
            {
                Day = b.DayOfWeek,
                DayName = Enum.GetName(typeof(DayOfWeek), b.DayOfWeek) ?? b.DayOfWeek.ToString(),
                Open = b.OpenTime.ToString(@"hh\:mm"),
                Close = b.CloseTime.ToString(@"hh\:mm"),
                Closed = b.IsClosed
            }),
            Staffs = tenant.AppUsers?.Select(u => new
            {
                Id = u.AppUserID,
                FullName = $"{u.FirstName} {u.LastName}",
                Specialization = u.Specialization
            }),
            TodaySchedule = todayAppointments
        };

        return Ok(megaContext);
    }

    // ─── Google Email / Token güncelleme — Ownership kontrolü ────────────────

    public class UpdateGoogleEmailRequest
    {
        public int TenantId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Token { get; set; }
    }

    [HttpPost("UpdateGoogleEmail")]
    public async Task<IActionResult> UpdateGoogleEmail([FromBody] UpdateGoogleEmailRequest request)
    {
        // Sadece Admin veya ilgili tenant sahibi yapabilir
        if (!IsAdmin() && GetCurrentTenantId() != request.TenantId)
            return StatusCode(403, new { Message = "Bu işletme için güncelleme yetkiniz yok." });

        var tenant = await _tenantService.GetByIdAsync(request.TenantId);
        if (tenant == null) return NotFound();

        tenant.GoogleEmail = request.Email;
        if (!string.IsNullOrEmpty(request.Token))
            tenant.GoogleAccessToken = request.Token;

        await _tenantService.UpdateAsync(tenant);
        return Ok(new { Message = "Email ve Token güncellendi." });
    }

    // ─── Bot durumu güncelleme — Ownership kontrolü ───────────────────────────

    [HttpPost("UpdateBotStatus")]
    public async Task<IActionResult> UpdateBotStatus([FromQuery] int tenantId, [FromQuery] bool isBotActive)
    {
        if (!IsAdmin() && GetCurrentTenantId() != tenantId)
            return StatusCode(403, new { Message = "Bu işletme için güncelleme yetkiniz yok." });

        var tenant = await _tenantService.GetByIdAsync(tenantId);
        if (tenant == null) return NotFound();

        tenant.IsBotActive = isBotActive;
        await _tenantService.UpdateAsync(tenant);

        return Ok(new { Message = "AI asistan durumu güncellendi.", IsBotActive = tenant.IsBotActive });
    }

    // ─── Abonelik iptali — Ownership kontrolü ────────────────────────────────

    [HttpPost("{id}/cancel-subscription")]
    public async Task<IActionResult> CancelSubscription(int id)
    {
        if (!IsAdmin() && GetCurrentTenantId() != id)
            return StatusCode(403, new { Message = "Bu işletmenin aboneliğini iptal etme yetkiniz yok." });

        var tenant = await _tenantService.GetByIdAsync(id);
        if (tenant == null) return NotFound(new { message = "İşletme bulunamadı." });

        if (string.IsNullOrWhiteSpace(tenant.SubscriptionReferenceCode))
            return BadRequest(new { message = "Bu işletme için abonelik referansı bulunamadı." });

        await _iyzicoPaymentService.CancelSubscriptionAsync(tenant.SubscriptionReferenceCode);

        tenant.IsActive = false;
        tenant.IsSubscriptionActive = false;
        await _tenantService.UpdateAsync(tenant);

        return Ok(new { status = "cancelled" });
    }

    // ─── Google Access Token yenileme (n8n için) ─────────────────────────────
    /// <summary>
    /// GET /api/Tenants/GetGoogleAccessToken?instanceName=appointment
    /// Tenant'ın refresh token'ı ile Google'dan taze access token alır.
    /// WebhookAuthMiddleware tarafından X-Auth-Token ile korunur.
    /// </summary>

    [AllowAnonymous]
    [HttpGet("GetGoogleAccessToken")]
    public async Task<IActionResult> GetGoogleAccessToken([FromQuery] string instanceName, [FromQuery] int? staffId = null)
    {
        var tenant = await _tenantService.GetContextByInstanceAsync(instanceName);
        if (tenant == null)
            return NotFound(new { error = "İşletme bulunamadı." });

        if (!tenant.IsActive || !tenant.IsSubscriptionActive)
            return StatusCode(403, new { error = "Pasif işletme için token üretilemiyor." });

        string? refreshToken = tenant.GoogleAccessToken;
        string? email = tenant.GoogleEmail;

        if (staffId.HasValue && staffId.Value > 0)
        {
            var staff = await _appUserService.GetAllUsersAsync();
            var user = staff.FirstOrDefault(u => u.AppUserID == staffId.Value && u.TenantID == tenant.TenantID);
            
            if (user == null) return NotFound(new { error = "Personel bulunamadı." });
            
            if (string.IsNullOrEmpty(user.GoogleRefreshToken))
                return BadRequest(new { error = "Bu personelin Google hesabı bağlı değil." });
                
            refreshToken = user.GoogleRefreshToken;
            email = user.GoogleCalendarId;
        }
        else
        {
            if (string.IsNullOrEmpty(tenant.GoogleAccessToken))
                return BadRequest(new { error = "Bu işletmenin Google hesabı bağlı değil." });

            if (string.IsNullOrEmpty(tenant.GoogleEmail))
                return BadRequest(new { error = "Google email bilgisi eksik." });
        }

        try
        {
            var clientId = _configuration["Google:ClientId"];
            var clientSecret = _configuration["Google:ClientSecret"];

            var httpClient = _httpClientFactory.CreateClient();

            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", clientId! },
                { "client_secret", clientSecret! },
                { "refresh_token", refreshToken },
                { "grant_type", "refresh_token" }
            });

            var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return StatusCode(502, new { error = "Google token yenilemesi başarısız.", detail = responseBody });

            var tokenData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseBody);
            var accessToken = tokenData.GetProperty("access_token").GetString();

            return Ok(new
            {
                accessToken,
                email = email,
                calendarId = email
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Token yenileme hatası.", detail = ex.Message });
        }
    }
}