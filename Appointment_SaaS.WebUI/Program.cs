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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddMemoryCache();

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// AutoMapper
var mapperConfig = new MapperConfiguration(mc =>
{
    mc.AddProfile(new MappingProfile());
});
IMapper mapper = mapperConfig.CreateMapper();
builder.Services.AddSingleton(mapper);

// Evolution API Settings
builder.Services.Configure<EvolutionApiSettings>(builder.Configuration.GetSection("EvolutionApi"));

// HttpClient for API communication
builder.Services.AddHttpClient("Api", client =>
{
    var baseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5294";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(40);
    
    // API tarafinda WebhookAuthMiddleware, POST firlatilinca Token istiyor.
    // Dashboard icerisindeki manuel eklemeler form araciligi ile HttpClient uzerinden aktigindan
    // bu token'i daimi HttpClient defaultlarina ekliyoruz.
    var webhookToken = builder.Configuration["WebhookSecurity:N8nAuthToken"] ?? "dev-webhook-token";
    client.DefaultRequestHeaders.Add("X-Auth-Token", webhookToken);
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
builder.Services.AddScoped<ITenantService, TenantManager>();
builder.Services.AddScoped<IAppUserService, AppUserManager>();
builder.Services.AddScoped<IAuditLogService, AuditLogManager>();
builder.Services.AddScoped<IFeedbackService, FeedbackManager>();
builder.Services.AddHttpContextAccessor(); // IP ve User bilgilerini yakalamak için şart!

// WebUI API Services
builder.Services.AddScoped<Appointment_SaaS.WebUI.Services.Abstract.IGoogleCalendarApiService, Appointment_SaaS.WebUI.Services.Concrete.GoogleCalendarApiService>();
builder.Services.AddScoped<Appointment_SaaS.WebUI.Services.Abstract.IDashboardApiService, Appointment_SaaS.WebUI.Services.Concrete.DashboardApiService>();
builder.Services.AddScoped<Appointment_SaaS.WebUI.Services.Abstract.IAppointmentApiService, Appointment_SaaS.WebUI.Services.Concrete.AppointmentApiService>();

// Auth (Adding cookie auth so User.FindFirst works)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Anlık Kesinti: Her istekte tenant aktifliğini kontrol et
app.UseActiveTenantCheck();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Pricing}/{action=Index}/{id?}");

app.Run();
