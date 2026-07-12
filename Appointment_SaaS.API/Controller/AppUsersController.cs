using Appointment_SaaS.API.Authorization;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Constants;
using Appointment_SaaS.Core.DTOs;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Services;
using Appointment_SaaS.Core.Utilities;
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
    private readonly IUserOperationClaimService _userOperationClaimService;

    public AppUsersController(
        IAppUserService appUserService,
        IAuthService authService,
        ITenantService tenantService,
        ITenantProvider tenantProvider,
        IUserOperationClaimService userOperationClaimService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _appUserService = appUserService;
        _authService = authService;
        _tenantService = tenantService;
        _tenantProvider = tenantProvider;
        _userOperationClaimService = userOperationClaimService;
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
        return Ok(users.Select(StaffListItemDto.FromEntity));
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
        else
        {
            var scopeDenied = ControllerTenantAccess.DenyUnlessCanAccessTenant(this, tenantId);
            if (scopeDenied != null)
                return scopeDenied;
        }

        var users = await _appUserService.GetStaffByTenantAsync(tenantId);
        return Ok(users.Select(StaffListItemDto.FromEntity));
    }

    // POST /api/AppUsers/add-staff
    [HttpPost("add-staff")]
    public async Task<IActionResult> AddStaff([FromBody] AddStaffDto dto)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId == null) return Unauthorized();

        var tenant = await _tenantService.GetByIdAsync(tenantId.Value);
        if (tenant == null) return NotFound(new { Message = "İşletme bulunamadı." });

        var currentStaffCount = await _appUserService.GetActiveStaffCountAsync(tenantId.Value);
        var isFirstStaff = currentStaffCount == 0;

        var additionalStaffCount = isFirstStaff ? 0 : currentStaffCount - 1; // İlk personel = Manager; kotaya dahil değil
        var staffLimit = PlanPricing.GetStaffLimit(tenant.PlanType);

        if (!isFirstStaff && staffLimit == 0)
            return StatusCode(403, new
            {
                Message = "Deneme planında personel ekleyemezsiniz. Lütfen bir plan seçin.",
                UpgradeRequired = true,
                CurrentPlan = tenant.PlanType
            });

        if (!isFirstStaff && additionalStaffCount >= staffLimit)
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
            Status = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var userId = await _appUserService.AddAppUserAsync(newUser);
        if (isFirstStaff)
        {
            await _userOperationClaimService.AddAsync(new UserOperationClaim
            {
                UserId = userId,
                OperationClaimId = 2
            });
        }

        return Ok(new
        {
            Message = isFirstStaff
                ? "İlk personel (yönetici) eklendi."
                : "Personel başarıyla eklendi.",
            UserId = userId
        });
    }

    /// <summary>
    /// Kayıt sonrası ilk personel (Manager). Tenant'ta aktif AppUser yokken; telefon işletme kaydı ile eşleşmeli.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("bootstrap-first-staff")]
    public async Task<IActionResult> BootstrapFirstStaff([FromBody] BootstrapFirstStaffDto dto)
    {
        if (dto.TenantId <= 0)
            return BadRequest(new { Message = "Geçersiz işletme." });

        var tenant = await _tenantService.GetByIdAsync(dto.TenantId);
        if (tenant == null)
            return NotFound(new { Message = "İşletme bulunamadı." });

        var activeCount = await _appUserService.GetActiveStaffCountAsync(dto.TenantId);
        if (activeCount > 0)
            return BadRequest(new { Message = "Bu işletmede zaten personel var. Giriş yapıp Personel Ekle kullanın." });

        var tenantPhone = OtpPhoneNormalizer.Normalize(tenant.PhoneNumber);
        var dtoPhone = OtpPhoneNormalizer.Normalize(dto.PhoneNumber);
        if (string.IsNullOrEmpty(tenantPhone) || tenantPhone != dtoPhone)
            return BadRequest(new { Message = "Telefon numarası işletme kaydı ile eşleşmiyor." });

        var existingByPhone = await _appUserService.GetByPhoneNumberAsync(dtoPhone);
        if (existingByPhone != null)
            return BadRequest(new { Message = "Bu telefon numarası zaten kayıtlı." });

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var existingByEmail = await _appUserService.GetByMail(dto.Email);
            if (existingByEmail != null)
                return BadRequest(new { Message = "Bu e-posta adresi zaten kayıtlı." });
        }

        var newUser = new AppUser
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName?.Trim() ?? "",
            Email = dto.Email?.Trim() ?? "",
            PhoneNumber = dtoPhone,
            Specialization = dto.Specialization,
            TenantID = dto.TenantId,
            Status = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var userId = await _appUserService.AddAppUserAsync(newUser);
        await _userOperationClaimService.AddAsync(new UserOperationClaim
        {
            UserId = userId,
            OperationClaimId = 2
        });

        return Ok(new { Message = "İlk personel kaydedildi. OTP ile giriş yapabilirsiniz.", UserId = userId });
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

        var freshUser = await _appUserService.GetByIdAsync(id);
        if (freshUser == null) return NotFound();
        freshUser.GoogleCalendarId = dto.Email;
        freshUser.GoogleRefreshToken = dto.Token;
        await _appUserService.UpdateAsync(freshUser);
        return Ok(new { Message = "Personelin Google Takvim bilgileri güncellendi." });
    }

    [Authorize(Roles = "Manager")]
    [HttpGet("{id}/google-token")]
    public async Task<IActionResult> GetGoogleToken(int id)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId == null)
            return Unauthorized();

        var users = await _appUserService.GetStaffByTenantAsync(tenantId.Value);
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