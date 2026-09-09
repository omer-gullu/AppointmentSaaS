using Appointment_SaaS.Business.Abstract;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace Appointment_SaaS.API.Authorization;

/// <summary>
/// JWT ile giriş yapmış panel kullanıcısının işletmesinin operasyonel erişimini (abonelik/askı)
/// API katmanında da zorlar. Panel middleware'ine ek savunma derinliği: geçerli JWT'ye sahip
/// askıdaki bir tenant, token ömrü boyunca API'ye DOĞRUDAN erişemesin.
///
/// Kapsam dışı (bilinçli):
///  • Admin  → her zaman geçer.
///  • Webhook (n8n) istekleri → kendi tenant kapsamı + ilgili uçlarda EvaluateOperationalAccess zaten var.
///  • TenantId claim'i olmayan (anonim / n8n) istekler → dokunulmaz.
///  • Auth ve faturalandırma/plan uçlarına UYGULANMAZ (askıdaki tenant yeniden abone olabilsin).
///
/// Sonuç 402/403 ise isteği kısa devre yapar. Aktif tenant'lar için 60 sn cache ile DB yükü sınırlanır.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireActiveTenantAttribute : Attribute, IAsyncActionFilter
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);
    private const string CacheKeyPrefix = "api_tenant_access_";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;

        // Admin veya webhook (n8n) → dokunma.
        if (ControllerTenantAccess.IsAdmin(http.User) || ControllerTenantAccess.IsWebhook(http.User))
        {
            await next();
            return;
        }

        // Webhook middleware tenant kapsamı set etmişse (n8n JWT değil) → dokunma.
        if (ControllerTenantAccess.GetWebhookScopedTenantId(http).HasValue)
        {
            await next();
            return;
        }

        // Yalnızca JWT tenant claim'i olan panel kullanıcıları denetlenir.
        if (!ControllerTenantAccess.TryGetClaimTenantId(http.User, out var tenantId))
        {
            await next();
            return;
        }

        var cache = http.RequestServices.GetRequiredService<IMemoryCache>();
        var cacheKey = $"{CacheKeyPrefix}{tenantId}";

        if (!cache.TryGetValue(cacheKey, out bool isAllowed))
        {
            var tenantService = http.RequestServices.GetRequiredService<ITenantService>();
            var access = await tenantService.EvaluateOperationalAccessAsync(tenantId);
            isAllowed = access.IsAllowed;

            // Erişim yoksa cache'lemeden hemen kısa devre (reaktivasyon anında yansısın).
            if (!isAllowed)
            {
                context.Result = new ObjectResult(new { access.Message })
                {
                    StatusCode = access.SuggestedStatusCode
                };
                return;
            }

            cache.Set(cacheKey, true, CacheDuration);
        }

        if (!isAllowed)
        {
            context.Result = new ObjectResult(new { Message = "İşletme erişimi askıya alınmış." })
            {
                StatusCode = StatusCodes.Status402PaymentRequired
            };
            return;
        }

        await next();
    }
}
