using Appointment_SaaS.WebUI.Diagnostics;
using Appointment_SaaS.WebUI.Middlewares;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Business.Mapping;
using Appointment_SaaS.Core.Utilities;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Concrete;
using Appointment_SaaS.Data.Context;
using Appointment_SaaS.DataAccess.Abstract;
using AutoMapper;
using Appointment_SaaS.WebUI.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "image/svg+xml",
        "application/javascript",
        "text/css"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);

// DbContext (pool reduces per-request context allocation — helps TTFB under load)
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

// AutoMapper
var mapperConfig = new MapperConfiguration(mc =>
{
    mc.AddProfile(new MappingProfile());
});
IMapper mapper = mapperConfig.CreateMapper();
builder.Services.AddSingleton(mapper);

// Evolution API Settings
builder.Services.Configure<EvolutionApiSettings>(builder.Configuration.GetSection("EvolutionApi"));
builder.Services.Configure<SubscriptionBillingOptions>(
    builder.Configuration.GetSection(SubscriptionBillingOptions.SectionName));

// HttpClient for API communication
builder.Services.AddHttpClient("Api", client =>
{
    var baseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5294";
    client.BaseAddress = new Uri(baseUrl);
    // Kayıt: Evolution + İyzico checkout tek istekte 40 sn'yi aşabiliyor
    client.Timeout = TimeSpan.FromSeconds(180);
});

// Typed HttpClient for Evolution API
builder.Services.AddHttpClient<IEvolutionApiService, EvolutionApiManager>(client =>
{
    var evoSettings = builder.Configuration.GetSection("EvolutionApi").Get<EvolutionApiSettings>();
    if (evoSettings != null && Uri.TryCreate(evoSettings.BaseUrl, UriKind.Absolute, out var validatedUri))
    {
        client.BaseAddress = validatedUri;
    }
});

// Repository kayıtları
builder.Services.AddScoped<ITenantRepository, EfTenantRepository>();
builder.Services.AddScoped<IAppUserRepository, EfAppUserRepository>();
builder.Services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
builder.Services.AddScoped<IFeedbackRepository, EfFeedbackRepository>();

// Business Services
builder.Services.AddSingleton<ITenantAccessEvaluator, TenantAccessEvaluator>();
builder.Services.AddScoped<ITenantService, TenantManager>();
builder.Services.AddScoped<IAppUserService, AppUserManager>();
builder.Services.AddScoped<IAuditLogService, AuditLogManager>();
builder.Services.AddScoped<IFeedbackService, FeedbackManager>();
builder.Services.AddHttpContextAccessor(); // IP ve User bilgilerini yakalamak için şart!

// WebUI API Services
builder.Services.AddScoped<Appointment_SaaS.WebUI.Services.Abstract.IGoogleCalendarApiService, Appointment_SaaS.WebUI.Services.Concrete.GoogleCalendarApiService>();
builder.Services.AddScoped<Appointment_SaaS.WebUI.Services.Abstract.IDashboardApiService, Appointment_SaaS.WebUI.Services.Concrete.DashboardApiService>();
builder.Services.AddScoped<Appointment_SaaS.WebUI.Services.Abstract.IAppointmentApiService, Appointment_SaaS.WebUI.Services.Concrete.AppointmentApiService>();
builder.Services.AddScoped<Appointment_SaaS.WebUI.Services.Abstract.IWhatsAppBlockedPhoneApiService, Appointment_SaaS.WebUI.Services.Concrete.WhatsAppBlockedPhoneApiService>();

// Auth (Adding cookie auth so User.FindFirst works)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

var app = builder.Build();

PerfProbeLog.Configure(Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "debug-4e7483.log")));

var subscriptionBilling = app.Configuration
    .GetSection(SubscriptionBillingOptions.SectionName)
    .Get<SubscriptionBillingOptions>() ?? new SubscriptionBillingOptions();
SubscriptionAccessPolicy.Configure(subscriptionBilling);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseResponseCompression();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var headers = ctx.Context.Response.Headers;
        if (!headers.ContainsKey("Cache-Control"))
        {
            headers["Cache-Control"] = "public,max-age=604800";
        }
    }
});

app.UseRouting();

app.UseResponseCaching();

app.UseSecurityHeaders();

app.UseAuthentication();
app.UseAuthorization();

// Anlık Kesinti: Her istekte tenant aktifliğini kontrol et
app.UseActiveTenantCheck();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Pricing}/{action=Index}/{id?}");

app.Run();
