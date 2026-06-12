using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;



namespace Appointment_SaaS.API.Authorization;



/// <summary>

/// JWT ve webhook için tenant ownership.

/// </summary>

public static class ControllerTenantAccess

{

    public static bool IsWebhook(ClaimsPrincipal? user) =>

        user?.Identity?.AuthenticationType == "WebhookScheme";



    public static bool IsAdmin(ClaimsPrincipal? user) =>

        user?.IsInRole("Admin") == true;



    public static bool TryGetClaimTenantId(ClaimsPrincipal? user, out int tenantId)

    {

        tenantId = 0;

        var claim = user?.FindFirst("TenantId")?.Value;

        return int.TryParse(claim, out tenantId) && tenantId > 0;

    }



    public static int? GetWebhookScopedTenantId(HttpContext? httpContext)

    {

        if (httpContext?.Items.TryGetValue(WebhookContextKeys.TenantId, out var value) == true

            && value is int tenantId

            && tenantId > 0)

            return tenantId;

        return null;

    }



    /// <summary>

    /// Webhook isteğinin middleware'de doğrulanmış tenant kapsamı ile kaynak tenant'ı eşleşmeli.

    /// </summary>

    public static IActionResult? DenyUnlessWebhookScopedToTenant(ControllerBase controller, int resourceTenantId)

    {

        if (!IsWebhook(controller.User))

            return null;



        var scoped = GetWebhookScopedTenantId(controller.HttpContext);

        if (!scoped.HasValue || scoped.Value != resourceTenantId)

        {

            return controller.StatusCode(403, new

            {

                Message = "Webhook isteği bu işletme kapsamında değil. X-Tenant-Id ve işletme ApiKey kullanın."

            });

        }



        return null;

    }



    /// <summary>

    /// Kaynak tenant'ına erişim yoksa 403; izin varsa null döner.

    /// </summary>

    public static IActionResult? DenyUnlessCanAccessTenant(ControllerBase controller, int resourceTenantId)

    {

        if (IsAdmin(controller.User))

            return null;



        var webhookScopedTenant = GetWebhookScopedTenantId(controller.HttpContext);

        if (IsWebhook(controller.User) || webhookScopedTenant.HasValue)

        {

            if (!webhookScopedTenant.HasValue || webhookScopedTenant.Value != resourceTenantId)

                return controller.StatusCode(403, new { Message = "Bu işletmeye ait kaynağa erişim yetkiniz yok." });

            return null;

        }



        if (!TryGetClaimTenantId(controller.User, out var claimTenantId) || claimTenantId != resourceTenantId)

            return controller.StatusCode(403, new { Message = "Bu işletmeye ait kaynağa erişim yetkiniz yok." });



        return null;

    }



    /// <summary>

    /// Manager için DTO tenant'ını JWT claim ile sabitler. Admin dokunmaz.

    /// </summary>

    public static IActionResult? EnforceDtoTenantForManager(ControllerBase controller, Action<int> setTenantId)

    {

        if (IsAdmin(controller.User))

            return null;



        if (IsWebhook(controller.User))

        {

            var scoped = GetWebhookScopedTenantId(controller.HttpContext);

            if (!scoped.HasValue)

                return controller.StatusCode(403, new { Message = "Webhook tenant kapsamı bulunamadı." });

            setTenantId(scoped.Value);

            return null;

        }



        if (!TryGetClaimTenantId(controller.User, out var claimTenantId))

            return controller.Unauthorized();



        setTenantId(claimTenantId);

        return null;

    }

}

