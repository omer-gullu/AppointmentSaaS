using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Constants;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,Manager")]
public class AppUsersController : ControllerBase
{
    private readonly IAppUserService _appUserService;
    private readonly IAuthService _authService;
    private readonly ITenantService _tenantService;
    private readonly ITenantProvider _tenantProvider;

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public AppUsersController(
        IAppUserService appUserService,
        IAuthService authService,
        ITenantService tenantService,
        ITenantProvider tenantProvider,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _appUserService = appUserService;
        _authService = authService;
        _tenantService = tenantService;
        _tenantProvider = tenantProvider;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    // GET /api/AppUsers — Tenant'ın tüm personeli
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId == null) return Unauthorized();

        var users = await _appUserService.GetStaffByTenantAsync(tenantId.Value);
        return Ok(users);
    }

    [Authorize(AuthenticationSchemes = "Bearer,WebhookScheme")]
    [HttpGet("staff/{tenantId}")]
    public async Task<IActionResult> GetStaffByTenant(int tenantId)
    {
        var isWebhook = User.Identity?.AuthenticationType == "WebhookScheme";

        if (!isWebhook)
        {
            var currentTenantId = _tenantProvider.GetTenantId();
            if (currentTenantId == null) return Unauthorized();
            if (currentTenantId.Value != tenantId) return Forbid();
        }

        var users = await _appUserService.GetStaffByTenantAsync(tenantId);
        return Ok(users.Select(u => new
        {
            appUserID = u.AppUserID,
            firstName = u.FirstName,
            lastName = u.LastName,
            email = u.Email,
            phoneNumber = u.PhoneNumber,
            specialization = u.Specialization,
            googleCalendarId = u.GoogleCalendarId,
            googleRefreshToken = !string.IsNullOrEmpty(u.GoogleRefreshToken) ? "***" : null,
            status = u.Status
        }));
    }

    // POST /api/AppUsers/add-staff
    [HttpPost("add-staff")]
    public async Task<IActionResult> AddStaff([FromBody] AddStaffDto dto)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId == null) return Unauthorized();

        var tenant = await _tenantService.GetByIdAsync(tenantId.Value);
        if (tenant == null) return NotFound(new { Message = "İşletme bulunamadı." });

        // Plan limiti kontrolü
        var currentStaffCount = await _appUserService.GetActiveStaffCountAsync(tenantId.Value);
        var staffOnly = currentStaffCount - 1; // Owner zaten 1 kişi
        var staffLimit = PlanPricing.GetStaffLimit(tenant.PlanType);

        if (staffLimit == 0)
            return StatusCode(403, new
            {
                Message = "Deneme planında personel ekleyemezsiniz. Lütfen bir plan seçin.",
                UpgradeRequired = true,
                CurrentPlan = tenant.PlanType
            });

        if (staffOnly >= staffLimit)
            return StatusCode(403, new
            {
                Message = $"{tenant.PlanType} planda en fazla {staffLimit} personel ekleyebilirsiniz. Daha fazlası için planınızı yükseltin.",
                UpgradeRequired = tenant.PlanType?.ToLower() != "pro",
                CurrentPlan = tenant.PlanType,
                Limit = staffLimit
            });

        // Duplicate kontrolü
        var existingByPhone = await _appUserService.GetByPhoneNumberAsync(dto.PhoneNumber);
        if (existingByPhone != null)
            return BadRequest(new { Message = "Bu telefon numarası zaten kayıtlı." });

        var existingByEmail = await _appUserService.GetByMail(dto.Email);
        if (existingByEmail != null)
            return BadRequest(new { Message = "Bu e-posta adresi zaten kayıtlı." });

        var newUser = new AppUser
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            Specialization = dto.Specialization,
            GoogleCalendarId = dto.GoogleCalendarId,
            TenantID = tenantId.Value,
            Status = true
        };

        var userId = await _appUserService.AddAppUserAsync(newUser);
        return Ok(new { Message = "Personel başarıyla eklendi.", UserId = userId });
    }

    // PUT /api/AppUsers/{id} — Personel güncelle
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateStaff(int id, [FromBody] UpdateStaffDto dto)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId == null) return Unauthorized();

        var users = await _appUserService.GetStaffByTenantAsync(tenantId.Value);
        var user = users.FirstOrDefault(u => u.AppUserID == id);

        if (user == null)
            return NotFound(new { Message = "Personel bulunamadı." });

        // Başka tenant'ın personelini güncellemeye çalışıyor mu?
        if (user.TenantID != tenantId.Value)
            return Forbid();

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Email = dto.Email;
        user.PhoneNumber = dto.PhoneNumber;
        user.Specialization = dto.Specialization;
        user.GoogleCalendarId = dto.GoogleCalendarId;
        user.Status = dto.Status;

        await _appUserService.UpdateAsync(user);
        return Ok(new { Message = "Personel başarıyla güncellendi." });
    }

    // DELETE /api/AppUsers/{id} — Personel pasife al (soft delete)
    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveStaff(int id)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId == null) return Unauthorized();

        var users = await _appUserService.GetStaffByTenantAsync(tenantId.Value);
        var user = users.FirstOrDefault(u => u.AppUserID == id);

        if (user == null)
            return NotFound(new { Message = "Personel bulunamadı." });

        if (user.TenantID != tenantId.Value)
            return Forbid();

        await _appUserService.DeleteAsync(user);
        return Ok(new { Message = "Personel pasife alındı." });
    }

    public class UpdateStaffGoogleTokenDto
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }

    [HttpPost("{id}/google-token")]
    public async Task<IActionResult> UpdateGoogleToken(int id, [FromBody] UpdateStaffGoogleTokenDto dto)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId == null) return Unauthorized();

        var users = await _appUserService.GetStaffByTenantAsync(tenantId.Value);
        var user = users.FirstOrDefault(u => u.AppUserID == id);

        if (user == null)
            return NotFound(new { Message = "Personel bulunamadı." });

        if (user.TenantID != tenantId.Value)
            return Forbid();

        user.GoogleCalendarId = dto.Email;
        user.GoogleRefreshToken = dto.Token;
        await _appUserService.UpdateAsync(user);

        return Ok(new { Message = "Personelin Google Takvim bilgileri güncellendi." });
    }

    [AllowAnonymous]
    [HttpGet("{id}/google-token")]
    public async Task<IActionResult> GetGoogleToken(int id)
    {
        var users = await _appUserService.GetAllUsersAsync();
        var user = users.FirstOrDefault(u => u.AppUserID == id);

        if (user == null || string.IsNullOrWhiteSpace(user.GoogleRefreshToken) || string.IsNullOrWhiteSpace(user.GoogleCalendarId))
            return BadRequest(new { error = "Personelin Google hesabı bağlı değil." });

        try
        {
            var clientId = _configuration["Google:ClientId"];
            var clientSecret = _configuration["Google:ClientSecret"];

            var httpClient = _httpClientFactory.CreateClient();

            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", clientId! },
                { "client_secret", clientSecret! },
                { "refresh_token", user.GoogleRefreshToken },
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
                email = user.GoogleCalendarId,
                calendarId = user.GoogleCalendarId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Token yenileme hatası.", detail = ex.Message });
        }
    }
}