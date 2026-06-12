using Appointment_SaaS.API.Authorization;



namespace Appointment_SaaS.API.Middleware;



/// <summary>

/// n8n webhook istekleri: sistem yolları N8nAuthToken; tenant yolları Tenant.ApiKey + kapsam.

/// JWT ile giriş yapmış panel kullanıcıları muaf.

/// </summary>

public class WebhookAuthMiddleware

{

    private readonly RequestDelegate _next;

    private readonly ILogger<WebhookAuthMiddleware> _logger;



    public WebhookAuthMiddleware(RequestDelegate next, ILogger<WebhookAuthMiddleware> logger)

    {

        _next = next;

        _logger = logger;

    }



    public async Task InvokeAsync(HttpContext context, WebhookAuthValidator validator)

    {

        var path = context.Request.Path.Value ?? "";

        var method = context.Request.Method;



        if (!WebhookProtectedPathEvaluator.RequiresWebhookToken(path, method))

        {

            await _next(context);

            return;

        }



        if (context.User?.Identity?.IsAuthenticated == true

            && context.User.Identity.AuthenticationType != "WebhookScheme")

        {

            await _next(context);

            return;

        }



        var result = await validator.EvaluateAsync(context, path, method);



        switch (result.Kind)

        {

            case WebhookAuthResultKind.NotApplicable:

            case WebhookAuthResultKind.AllowJwt:

                await _next(context);

                return;



            case WebhookAuthResultKind.AllowSystem:

                context.Items[WebhookContextKeys.IsSystemWebhook] = true;

                await _next(context);

                return;



            case WebhookAuthResultKind.AllowTenant:

                context.Items[WebhookContextKeys.TenantId] = result.TenantId;

                await _next(context);

                return;



            case WebhookAuthResultKind.MissingConfiguration:

                _logger.LogError("[WebhookAuth] {Message} Path={Path}", result.Message, path);

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                await context.Response.WriteAsJsonAsync(new { Message = result.Message });

                return;



            default:

                _logger.LogWarning(

                    "[WebhookAuth] Yetkisiz. IP={IP} Path={Path} Method={Method}",

                    context.Connection.RemoteIpAddress,

                    path,

                    method);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                await context.Response.WriteAsJsonAsync(new { Message = result.Message ?? "Yetkisiz webhook isteği." });

                return;

        }

    }

}



public static class WebhookAuthMiddlewareExtensions

{

    public static IApplicationBuilder UseWebhookAuth(this IApplicationBuilder builder)

        => builder.UseMiddleware<WebhookAuthMiddleware>();

}

