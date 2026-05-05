using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Appointment_SaaS.WebUI.Middlewares
{
    /// <summary>
    /// Her HTTP isteğinde kullanıcının bağlı olduğu işletmenin (Tenant) aktif olup olmadığını kontrol eder.
    /// IsActive == false ise → Cookie silinir, kullanıcı "Hesap Askıda" mesajıyla Login'e yönlendirilir.
    /// Anlık Kesinti: Admin panelinden bir dükkan pasife çekildiğinde, o dükkanın
    /// kullanıcıları bir sonraki tıklamalarında otomatik olarak çıkış yapar.
    /// 
    /// Performans: Tenant durumu 60 saniye boyunca IMemoryCache'de tutulur.
    /// Her request'te API çağrısı yapmak yerine cache'den okur.
    /// </summary>
    public class ActiveTenantMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;

        // Cache süresi: 60 saniye. Admin pasife çekince max 60 sn içinde kullanıcı çıkar.
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);
        private const string CacheKeyPrefix = "tenant_status_";

        public ActiveTenantMiddleware(RequestDelegate next, IMemoryCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Auth, Login, Pricing gibi public sayfaları atla
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

            // Kullanıcı giriş yapmamışsa kontrole gerek yok
            if (context.User?.Identity == null || !context.User.Identity.IsAuthenticated)
            {
                await _next(context);
                return;
            }

            // JWT Claim'den TenantId'yi al
            var tenantIdClaim = context.User.FindFirst("TenantId")?.Value;
            if (string.IsNullOrEmpty(tenantIdClaim) || !int.TryParse(tenantIdClaim, out int tenantId))
            {
                await _next(context);
                return;
            }

            // Cache'den oku — varsa API'ye gitme
            var cacheKey = $"{CacheKeyPrefix}{tenantId}";
            if (!_cache.TryGetValue(cacheKey, out TenantStatusCache? cachedStatus))
            {
                // Cache'de yok — API'ye git
                cachedStatus = await FetchTenantStatusAsync(context, tenantId);

                if (cachedStatus != null)
                {
                    _cache.Set(cacheKey, cachedStatus, CacheDuration);
                }
            }

            // Tenant durumunu kontrol et
            if (cachedStatus != null && (!cachedStatus.IsActive || cachedStatus.IsBlacklisted))
            {
                // Cache'i temizle — sonraki girişte taze veri gelsin
                _cache.Remove(cacheKey);

                // Anlık Kesinti: Cookie'yi sil, Login'e yönlendir
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.Redirect("/Auth/Login?suspended=true");
                return;
            }

            await _next(context);
        }

        private async Task<TenantStatusCache?> FetchTenantStatusAsync(HttpContext context, int tenantId)
        {
            try
            {
                var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
                var client = httpClientFactory.CreateClient("Api");

                var response = await client.GetAsync($"api/Tenants/{tenantId}");
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                var tenantObj = JsonDocument.Parse(json).RootElement;

                bool isActive = tenantObj.TryGetProperty("isActive", out var activeProp) && activeProp.GetBoolean();
                bool isBlacklisted = tenantObj.TryGetProperty("isBlacklisted", out var blackProp) && blackProp.GetBoolean();

                return new TenantStatusCache(isActive, isBlacklisted);
            }
            catch
            {
                // API erişilemezse null dön — engelleme yapma, güvenli tarafta kal
                return null;
            }
        }
    }

    /// <summary>
    /// Cache'de saklanan tenant durum bilgisi.
    /// </summary>
    public record TenantStatusCache(bool IsActive, bool IsBlacklisted);

    /// <summary>
    /// Middleware'i pipeline'a eklemek için extension method.
    /// Program.cs'de kullanımı: app.UseActiveTenantCheck();
    /// </summary>
    public static class ActiveTenantMiddlewareExtensions
    {
        public static IApplicationBuilder UseActiveTenantCheck(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ActiveTenantMiddleware>();
        }
    }
}