using Appointment_SaaS.Core.Utilities;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Business.Validation;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Concrete;
using Appointment_SaaS.Data.Context;
using Appointment_SaaS.DataAccess.Abstract;
using FluentValidation.AspNetCore;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Appointment_SaaS.Business.Mapping;
using Appointment_SaaS.API.Middleware;
using Appointment_SaaS.Core.Utilities.Security.JWT;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Appointment_SaaS.Core.Utilities.Security.Jwt;
using Appointment_SaaS.Core.Utilities.Security;
using Appointment_SaaS.Core.Services;
using Appointment_SaaS.API.Services;
using AspNetCoreRateLimit;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// --- CONTROLLER AYARLARI ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- RATE LIMITING ---
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// --- CORS ---
var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000", "http://localhost:5678" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("StrictCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// --- VERITABANI BAGLANTISI ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// --- JWT AUTHENTICATION AYARLARI ---
var tokenOptions = builder.Configuration.GetSection("TokenOptions").Get<TokenOptions>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = tokenOptions.Issuer,
            ValidAudience = tokenOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey),
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var stampClaim = context.Principal?.FindFirst("SecurityStamp")?.Value;

                if (int.TryParse(userIdClaim, out int userId))
                {
                    var user = await dbContext.AppUsers.FindAsync(userId);
                    if (user == null || user.SecurityStamp != stampClaim)
                    {
                        context.Fail("Güvenlik ihlali: Oturumunuz sonlandırılmıştır.");
                    }
                }
            }
        };

    })
.AddScheme<WebhookAuthenticationOptions, WebhookAuthenticationHandler>(
    "WebhookScheme", options => { });


// --- DIGER SERVISLER ---
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<AppointmentValidator>();
builder.Services.AddAutoMapper(typeof(MappingProfile));

// --- REPOSITORY KAYITLARI ---
builder.Services.AddScoped<ITenantRepository, EfTenantRepository>();
builder.Services.AddScoped<IAppointmentRepository, EfAppointmentRepository>();
builder.Services.AddScoped<ISectorRepository, EfSectorRepository>();
builder.Services.AddScoped<IServiceRepository, EfServiceRepository>();
builder.Services.AddScoped<IAppUserRepository, EfAppUserRepository>();
builder.Services.AddScoped<IFeedbackRepository, EfFeedbackRepository>();



// --- SERVICE KAYITLARI ---
builder.Services.Configure<EvolutionApiSettings>(builder.Configuration.GetSection("EvolutionApi"));
builder.Services.Configure<IyzicoSettings>(builder.Configuration.GetSection("IyzicoSettings"));
builder.Services.Configure<LockoutSettings>(builder.Configuration.GetSection("LockoutSettings"));
builder.Services.AddHttpClient<IEvolutionApiService, EvolutionApiManager>(client =>
{
    var settings = builder.Configuration.GetSection("EvolutionApi").Get<EvolutionApiSettings>();
    if (settings != null && Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var validatedUri))
    {
        client.BaseAddress = validatedUri;
    }
    client.Timeout = TimeSpan.FromSeconds(25);
});

builder.Services.AddScoped<ITenantService, TenantManager>();
builder.Services.AddScoped<IAppointmentService, AppointmentManager>();
builder.Services.AddScoped<ISectorService, SectorManager>();
builder.Services.AddScoped<IServiceService, ServiceManager>();
builder.Services.AddScoped<IAppUserService, AppUserManager>();
builder.Services.AddScoped<IAuthService, AuthManager>();
builder.Services.AddScoped<IIyzicoPaymentService, IyzicoPaymentManager>();
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarManager>();
builder.Services.AddScoped<IFeedbackService, FeedbackManager>();

// --- YENI EKLENEN SERVISLER (OTP & ROLLER) ---
builder.Services.AddScoped<IOtpService, OtpManager>();
builder.Services.AddScoped<IUserOperationClaimRepository, EfUserOperationClaimRepository>();
builder.Services.AddScoped<IUserOperationClaimService, UserOperationClaimManager>();

// --- AUDIT LOG ---
builder.Services.AddHttpContextAccessor(); // AuditLogManager için IP & User Claim erişimi
builder.Services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
builder.Services.AddScoped<IAuditLogService, AuditLogManager>();

// --- GUENLIK YARDIMCILARI ---
builder.Services.AddScoped<ITokenHelper, JwtHelper>();
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();

// --- TENANT PROVIDER (Scoped: Her HTTP request başına bir kez oluşturulur, performans kaybı yok) ---
builder.Services.AddScoped<ITenantProvider, TenantProvider>();

var app = builder.Build();

// --- MIDDLEWARE PIPELINE ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTPS yönlendirmesi sadece production'da aktif
// Development'ta HTTP→HTTPS redirect, HttpClient'ın Authorization header'ını silmesine sebep olur
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// SIRALAMA COK ONEMLI:
app.UseMiddleware<ExceptionMiddleware>();
app.UseIpRateLimiting();
app.UseCors("StrictCorsPolicy");

app.UseWebhookAuth();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();


