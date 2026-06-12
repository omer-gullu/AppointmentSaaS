using Appointment_SaaS.WebUI.Diagnostics;

namespace Appointment_SaaS.WebUI.Middleware;

public class ServerPerfProbeMiddleware
{
    private readonly RequestDelegate _next;

    public ServerPerfProbeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/Pricing", StringComparison.OrdinalIgnoreCase) &&
            path != "/" &&
            !path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        PerfProbeLog.Write("H-D", "ServerPerfProbeMiddleware", "server_response", new
        {
            path,
            status = context.Response.StatusCode,
            elapsedMs = sw.ElapsedMilliseconds,
            contentLength = context.Response.ContentLength
        });
    }
}

public static class ServerPerfProbeMiddlewareExtensions
{
    public static IApplicationBuilder UseServerPerfProbe(this IApplicationBuilder app)
        => app.UseMiddleware<ServerPerfProbeMiddleware>();
}
