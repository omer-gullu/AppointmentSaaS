using System.Text.RegularExpressions;

namespace Appointment_SaaS.API.Middleware;

/// <summary>
/// n8n ve Evolution API'den gelen webhook isteklerini doğrulayan güvenlik middleware'ı.
/// X-Auth-Token header'ı ile korunur.
/// </summary>
public class WebhookAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhookAuthMiddleware> _logger;

    // Regex ile eşleştirilen webhook path'leri (POST/PATCH)
    private static readonly Regex[] ProtectedWebhookPatterns =
    {
        new(@"^/api/appointments$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/appointments/\d+/google-event$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/appointments/lock$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/appointments/unlock$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/auditlogs/workflow-error$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // Tüm metodlarda token gerektiren kritik endpoint'ler
    private static readonly string[] ProtectedApiPaths =
    {
        "/api/tenants/getcontextbyinstance",
        "/api/tenants/getgoogleaccesstoken",
        "/api/appointments/customer/",
        "/api/appointments/tomorrow"
    };

    // Kendi güvenliğini sağlayan endpoint'ler — bu middleware'dan muaf
    private static readonly string[] ExcludedPaths =
    {
        "/api/iyzico/webhook"
    };

    public WebhookAuthMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<WebhookAuthMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;

        // Muaf endpoint'ler
        if (ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Kritik API path'leri — tüm metodlarda korunur
        bool isProtectedApiPath = ProtectedApiPaths
            .Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        // Webhook path'leri — POST veya PATCH
        bool isWebhookPath = (method == "POST" || method == "PATCH")
            && ProtectedWebhookPatterns.Any(r => r.IsMatch(path));

        bool isProtectedPath = isProtectedApiPath || isWebhookPath;

        // JWT ile authenticate olmuş panel kullanıcıları token kontrolünden muaf
        bool isAuthenticated = context.User?.Identity?.IsAuthenticated == true;

        if (isProtectedPath && !isAuthenticated)
        {
            var expectedToken = _configuration["WebhookSecurity:N8nAuthToken"];

            if (string.IsNullOrEmpty(expectedToken))
            {
                _logger.LogError(
                    "[WebhookAuth] N8nAuthToken yapılandırılmamış. Path={Path}", path);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(
                    new { Message = "Webhook güvenlik yapılandırması eksik." });
                return;
            }

            var providedToken = context.Request.Headers["X-Auth-Token"].FirstOrDefault();

            if (string.IsNullOrEmpty(providedToken) || providedToken != expectedToken)
            {
                _logger.LogWarning(
                    "[WebhookAuth] Yetkisiz istek. IP={IP} Path={Path} Method={Method} Token={TokenStatus}",
                    context.Connection.RemoteIpAddress,
                    path,
                    method,
                    string.IsNullOrEmpty(providedToken) ? "YOK" : "GEÇERSIZ");

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new { Message = "Yetkisiz istek. Geçerli bir X-Auth-Token header'ı gereklidir." });
                return;
            }

            _logger.LogInformation(
                "[WebhookAuth] İstek doğrulandı. Path={Path} Method={Method}", path, method);
        }

        await _next(context);
    }
}

public static class WebhookAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseWebhookAuth(this IApplicationBuilder builder)
        => builder.UseMiddleware<WebhookAuthMiddleware>();
}