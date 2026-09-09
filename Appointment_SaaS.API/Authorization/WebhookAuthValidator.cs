using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.API.Authorization;

public enum WebhookAuthResultKind
{
    NotApplicable,
    AllowJwt,
    AllowSystem,
    AllowTenant,
    MissingConfiguration,
    Unauthorized
}

public readonly record struct WebhookAuthResult(WebhookAuthResultKind Kind, int? TenantId = null, string? Message = null);

public class WebhookAuthValidator
{
    private readonly IConfiguration _configuration;
    private readonly ITenantService _tenantService;

    public WebhookAuthValidator(IConfiguration configuration, ITenantService tenantService)
    {
        _configuration = configuration;
        _tenantService = tenantService;
    }

    public async Task<WebhookAuthResult> EvaluateAsync(HttpContext context, string path, string method)
    {
        if (!WebhookProtectedPathEvaluator.RequiresWebhookToken(path, method))
            return new WebhookAuthResult(WebhookAuthResultKind.NotApplicable);

        // Sistem-only yollar (çok kiracılı cron: reminders/pending & reminders/run) TÜM
        // tenant'ların verisine dokunur ve toplu WhatsApp tetikler. Bu yüzden panel JWT'si
        // (ücretsiz trial dahil) bu yolları ATLAYAMAZ — yalnızca N8nAuthToken sistem token'ı
        // kabul edilir. JWT muafiyeti sadece tenant-kapsamlı webhook yolları içindir.
        var isSystemOnly = WebhookProtectedPathEvaluator.IsSystemOnlyPath(path, method);

        if (!isSystemOnly
            && context.User?.Identity?.IsAuthenticated == true
            && context.User.Identity.AuthenticationType != "WebhookScheme")
            return new WebhookAuthResult(WebhookAuthResultKind.AllowJwt);

        var providedToken = context.Request.Headers["X-Auth-Token"].FirstOrDefault();

        if (isSystemOnly)
            return ValidateSystemToken(providedToken);

        var tenantId = await WebhookTenantResolver.ResolveTenantIdAsync(context.Request, _tenantService);
        if (!tenantId.HasValue)
        {
            return new WebhookAuthResult(
                WebhookAuthResultKind.Unauthorized,
                Message: "Webhook isteği için X-Tenant-Id, tenantId veya instanceName ile işletme kapsamı gereklidir.");
        }

        var tenant = await _tenantService.GetByIdAsync(tenantId.Value);
        if (tenant == null)
        {
            return new WebhookAuthResult(
                WebhookAuthResultKind.Unauthorized,
                Message: "İşletme bulunamadı.");
        }

        if (WebhookTokenComparer.Matches(providedToken, tenant.ApiKey))
            return new WebhookAuthResult(WebhookAuthResultKind.AllowTenant, tenantId);

        if (WebhookProtectedPathEvaluator.AllowsSystemBootstrap(path, method))
        {
            var systemResult = ValidateSystemToken(providedToken);
            if (systemResult.Kind == WebhookAuthResultKind.AllowSystem)
                return new WebhookAuthResult(WebhookAuthResultKind.AllowTenant, tenantId);
        }

        return new WebhookAuthResult(
            WebhookAuthResultKind.Unauthorized,
            Message: "Geçersiz işletme entegrasyon anahtarı (X-Auth-Token).");
    }

    private WebhookAuthResult ValidateSystemToken(string? providedToken)
    {
        var systemToken = _configuration["WebhookSecurity:N8nAuthToken"];
        if (string.IsNullOrWhiteSpace(systemToken))
            return new WebhookAuthResult(WebhookAuthResultKind.MissingConfiguration, Message: "Webhook güvenlik yapılandırması eksik.");

        if (!WebhookTokenComparer.Matches(providedToken, systemToken))
            return new WebhookAuthResult(WebhookAuthResultKind.Unauthorized, Message: "Geçersiz sistem webhook token'ı.");

        return new WebhookAuthResult(WebhookAuthResultKind.AllowSystem);
    }
}
