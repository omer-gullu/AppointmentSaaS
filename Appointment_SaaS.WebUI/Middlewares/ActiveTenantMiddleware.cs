using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using Appointment_SaaS.WebUI.Services;

namespace Appointment_SaaS.WebUI.Middlewares;

/// <summary>
/// Her HTTP isteğinde JWT ile API üzerinden tenant abonelik / askı durumunu doğrular.
/// JWT süresi dolduğunda oturum sona erdi mesajı; gerçek askıda ayrı mesaj gösterilir.
/// </summary>
public class ActiveTenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);
    private const string CacheKeyPrefix = "session_access_";

    private enum SessionAccessStatus
    {
        Allowed,
        SessionExpired,
        AccessDenied
    }

    public ActiveTenantMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        IHttpContextAccessor httpContextAccessor)
    {
        _next = next;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.StartsWith("/auth/") ||
            path.StartsWith("/pricing") ||
            path.StartsWith("/home") ||
            path.StartsWith("/css") ||
            path.StartsWith("/js") ||
            path.StartsWith("/lib") ||
            path.StartsWith("/_") ||
            path == "/")
        {
            await _next(context);
            return;
        }

        if (context.User?.Identity == null || !context.User.Identity.IsAuthenticated)
        {
            await _next(context);
            return;
        }

        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tenantIdClaim = context.User.FindFirst("TenantId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(tenantIdClaim))
        {
            await _next(context);
            return;
        }

        var cacheKey = $"{CacheKeyPrefix}{userIdClaim}_{tenantIdClaim}";
        if (!_cache.TryGetValue(cacheKey, out SessionAccessStatus cachedStatus))
        {
            var fetched = await FetchSessionAccessStatusAsync(context);
            if (fetched == null)
            {
                await _next(context);
                return;
            }

            cachedStatus = fetched.Value;
            _cache.Set(cacheKey, cachedStatus, CacheDuration);
        }

        if (cachedStatus != SessionAccessStatus.Allowed)
        {
            _cache.Remove(cacheKey);
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var redirectUrl = cachedStatus == SessionAccessStatus.SessionExpired
                ? "/Auth/Login?sessionExpired=true"
                : "/Auth/Login?suspended=true";

            context.Response.Redirect(redirectUrl);
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// null: API doğrulanamadı — isteği engelleme.
    /// </summary>
    private async Task<SessionAccessStatus?> FetchSessionAccessStatusAsync(HttpContext context)
    {
        try
        {
            var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
            var client = httpClientFactory.CreateClient("Api");
            await HttpClientTokenHelper.AttachBearerTokenAsync(client, _httpContextAccessor);

            if (client.DefaultRequestHeaders.Authorization == null)
                return SessionAccessStatus.SessionExpired;

            var response = await client.GetAsync("api/Auth/session-access", context.RequestAborted);

            if (response.StatusCode == HttpStatusCode.NoContent)
                return SessionAccessStatus.Allowed;

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return SessionAccessStatus.SessionExpired;

            if ((int)response.StatusCode >= 500)
                return null;

            // 402/403: geçerli JWT ama işletme erişimi yok (askı, abonelik vb.)
            return SessionAccessStatus.AccessDenied;
        }
        catch
        {
            return null;
        }
    }
}

public static class ActiveTenantMiddlewareExtensions
{
    public static IApplicationBuilder UseActiveTenantCheck(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ActiveTenantMiddleware>();
    }
}
