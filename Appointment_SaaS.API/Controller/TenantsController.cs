using Appointment_SaaS.API.Authorization;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using Iyzipay.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    private readonly ITenantPlanService _tenantPlanService;

    public TenantsController(
        ITenantService tenantService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IIyzicoPaymentService iyzicoPaymentService,
        IAppUserService appUserService,
        ITenantPlanService tenantPlanService)
    {
        _tenantService = tenantService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _iyzicoPaymentService = iyzicoPaymentService;
        _appUserService = appUserService;
        _tenantPlanService = tenantPlanService;
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var tenants = await _tenantService.GetAllAsync();
        return Ok(tenants.Select(TenantAdminResponseDto.FromEntity));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!IsAdmin() && GetCurrentTenantId() != id)
            return StatusCode(403, new { Message = "Bu işletmeyi görüntüleme yetkiniz yok." });

        var tenant = await _tenantService.GetByIdWithBusinessHoursAsync(id);
        if (tenant == null) return NotFound();

        return Ok(IsAdmin()
            ? TenantAdminResponseDto.FromEntity(tenant)
            : TenantResponseDto.FromEntity(tenant));
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

        // IsActive, IsTrial ve SubscriptionEndDate yalnızca Admin tarafından değiştirilebilir
        if (IsAdmin())
        {
            tenant.IsActive = dto.IsActive;
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

    [HttpPost("update-break-time")]
    public async Task<IActionResult> UpdateBreakTime([FromBody] BreakTimeSettingsDto settings)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return Unauthorized(new { Message = "Geçersiz oturum." });

        try
        {
            await _tenantService.UpdateBreakTimeSettingsAsync(tenantId.Value, settings);
            return Ok(new { Message = "Mola saatleri başarıyla güncellendi." });
        }
        catch (BadHttpRequestException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Güncelleme sırasında hata oluştu: " + ex.Message });
        }
    }

    /// <summary>n8n entegrasyon anahtarı (Tenant.ApiKey). Yalnızca kendi işletmesi veya Admin.</summary>
    [HttpGet("integration-key")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> GetIntegrationKey()
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null)
            return Unauthorized(new { Message = "Geçersiz oturum." });

        var tenant = await _tenantService.GetByIdAsync(tenantId.Value);
        if (tenant == null)
            return NotFound(new { Message = "İşletme bulunamadı." });

        return Ok(new
        {
            TenantId = tenant.TenantID,
            IntegrationKey = tenant.ApiKey,
            Message = "n8n HTTP isteklerinde X-Auth-Token olarak IntegrationKey, X-Tenant-Id olarak TenantId gönderin. Hatırlatma cron'u için ayrıca sistem N8nAuthToken kullanılır."
        });
    }

    /// <summary>Mevcut entegrasyon anahtarını yeniler (canlıya geçişte zayıf anahtarları döndürmek için).</summary>
    [HttpPost("integration-key/rotate")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> RotateIntegrationKey()
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null)
            return Unauthorized(new { Message = "Geçersiz oturum." });

        var tenant = await _tenantService.GetByIdAsync(tenantId.Value);
        if (tenant == null)
            return NotFound(new { Message = "İşletme bulunamadı." });

        tenant.ApiKey = TenantIntegrationKeyGenerator.Create();
        await _tenantService.UpdateAsync(tenant);

        return Ok(new
        {
            TenantId = tenant.TenantID,
            IntegrationKey = tenant.ApiKey,
            Message = "Entegrasyon anahtarı yenilendi. n8n credential ve X-Auth-Token değerlerini güncelleyin."
        });
    }

    /// <summary>Ödeme sonrası plan değişimi — Trial veya ücretli tenant.</summary>
    [HttpPost("change-plan/init")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> InitializePlanChange([FromBody] ChangePlanInitRequestDto request)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null)
            return Unauthorized(new { Message = "Geçersiz oturum." });

        var tenant = await _tenantService.GetByIdAsync(tenantId.Value);
        if (tenant == null)
            return NotFound(new { Message = "İşletme bulunamadı." });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { Message = "Kullanıcı kimliği bulunamadı." });

        var owner = await _appUserService.GetByIdAsync(userId);
        if (owner == null || owner.TenantID != tenant.TenantID)
            return StatusCode(403, new { Message = "Plan değişikliği yalnızca işletme sahibi tarafından yapılabilir." });

        var callbackUrl = request.PaymentCallbackUrl?.Trim()
            ?? _configuration["WebUI:PaymentCallbackUrl"]
            ?? $"{Request.Scheme}://{Request.Host}/Auth/PaymentCallback";

        try
        {
            var result = await _tenantPlanService.InitializePlanChangeAsync(
                tenant, owner, request, callbackUrl);
            return Ok(result);
        }
        catch (BadHttpRequestException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("upgrade-plan")]
    [Authorize(Roles = "Admin")]
    [Obsolete("Ödeme sync'siz plan ataması kaldırıldı. POST change-plan/init kullanın.")]
    public IActionResult UpgradePlan([FromQuery] string planType, [FromQuery] int? tenantId = null)
    {
        return StatusCode(410, new
        {
            Message = "Bu endpoint devre dışı. Plan değişikliği yalnızca İyzico ödeme akışı (POST /api/Tenants/change-plan/init) ile yapılabilir.",
            Replacement = "/api/Tenants/change-plan/init"
        });
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

        var access = await _tenantService.EvaluateOperationalAccessAsync(tenant.TenantID);
        if (!access.IsAllowed)
            return StatusCode(access.SuggestedStatusCode, new { Message = access.Message });

        var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenant.TenantID);
        if (scopeDenied != null)
            return scopeDenied;

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
            IntegrationKey = tenant.ApiKey,
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
            BreakTime = new
            {
                Enabled = tenant.BreakTimeEnabled,
                Start = tenant.BreakStartTime.ToString(@"hh\:mm"),
                End = tenant.BreakEndTime.ToString(@"hh\:mm")
            },
            Staffs = tenant.AppUsers?.Where(u => u.Status == true).Select(u => new
            {
                Id = u.AppUserID,
                FullName = $"{u.FirstName} {u.LastName}",
                Specialization = u.Specialization,
            }),
            TodaySchedule = todayAppointments,

            // 🔴 YENİ
            UpcomingHolidays = tenant.Holidays?
            .OrderBy(h => h.Date)
            .Select(h => new
            {
                Date = h.Date.ToString("yyyy-MM-dd"),
                Name = h.Name
            })
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

        tenant.CancelAtPeriodEnd = true;
        tenant.AutoRenew = false;
        await _tenantService.UpdateAsync(tenant);

        return Ok(new
        {
            status = "cancel_at_period_end",
            subscriptionEndDate = tenant.SubscriptionEndDate,
            message = $"Abonelik yenilemesi iptal edildi. {tenant.SubscriptionEndDate:dd.MM.yyyy} tarihine kadar kullanmaya devam edebilirsiniz."
        });
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

        var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenant.TenantID);
        if (scopeDenied != null)
            return scopeDenied;

        string? refreshToken = tenant.GoogleAccessToken;
        string? email = tenant.GoogleEmail;

        if (staffId.HasValue && staffId.Value > 0)
        {
            var user = await _appUserService.GetByIdAsync(staffId.Value);
            if (user == null || user.TenantID != tenant.TenantID)
                return NotFound(new { error = "Personel bulunamadı." });
            
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

    [HttpGet("holidays")]
    public async Task<IActionResult> GetHolidays()
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return Unauthorized();
        var holidays = await _tenantService.GetHolidaysAsync(tenantId.Value);
        return Ok(holidays);
    }

    [HttpPost("holidays")]
    public async Task<IActionResult> AddHoliday([FromBody] HolidayCreateDto dto)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return Unauthorized();
        var holiday = await _tenantService.AddHolidayAsync(tenantId.Value, dto.Date, dto.Name);
        return Ok(new { Message = "Tatil eklendi.", holiday.Id });
    }

    [HttpDelete("holidays/{id}")]
    public async Task<IActionResult> DeleteHoliday(int id)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return Unauthorized();
        await _tenantService.DeleteHolidayAsync(tenantId.Value, id);
        return Ok(new { Message = "Tatil silindi." });
    }
}