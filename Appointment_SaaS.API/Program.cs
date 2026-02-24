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
using Appointment_SaaS.Business.Abstract;

var builder = WebApplication.CreateBuilder(args);

// --- CONTROLLER AYARLARI ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // FluentValidation hatalarýnýn senin yazdýðýn sýrayla gelmesi için burasý kritik!
        options.SuppressModelStateInvalidFilter = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- VERÝTABANI BAÐLANTISI ---
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
            // SecurityKeyHelper üzerinden anahtarýmýzý oluþturuyoruz
            IssuerSigningKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey)
        };
    });

// --- DÝÐER SERVÝSLER ---
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<AppointmentValidator>();
builder.Services.AddAutoMapper(typeof(MappingProfile));

// --- REPOSITORY KAYITLARI ---
builder.Services.AddScoped<ITenantRepository, EfTenantRepository>();
builder.Services.AddScoped<IAppointmentRepository, EfAppointmentRepository>();
builder.Services.AddScoped<ISectorRepository, EfSectorRepository>();
builder.Services.AddScoped<IServiceRepository, EfServiceRepository>();
builder.Services.AddScoped<IAppUserRepository, EfAppUserRepository>();

// --- SERVICE KAYITLARI ---
builder.Services.AddScoped<ITenantService, TenantManager>();
builder.Services.AddScoped<IAppointmentService, AppointmentManager>();
builder.Services.AddScoped<ISectorService, SectorManager>();
builder.Services.AddScoped<IServiceService, ServiceManager>();
builder.Services.AddScoped<IAppUserService, AppUserManager>();

// --- GÜVENLÝK YARDIMCILARI ---
builder.Services.AddScoped<ITokenHelper, JwtHelper>();

var app = builder.Build();

// --- MIDDLEWARE PIPELINE ---

// Hatalarý en tepede yakalayalým
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// SIRALAMA ÇOK ÖNEMLÝ:
app.UseAuthentication(); // Önce kimlik doðrula (Sen kimsin?)
app.UseAuthorization();  // Sonra yetki kontrol et (Buraya girebilir misin?)

app.MapControllers();

app.Run();