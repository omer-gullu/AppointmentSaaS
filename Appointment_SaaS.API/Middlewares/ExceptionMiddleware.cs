using Appointment_SaaS.Core.Utilities;
using System.Net;

namespace Appointment_SaaS.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(httpContext, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // ✅ Exception'ı logla — production'da debugging için kritik
        _logger.LogError(exception,
            "[UnhandledException] Path={Path} Method={Method} IP={IP}",
            context.Request.Path,
            context.Request.Method,
            context.Connection.RemoteIpAddress);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        // Kullanıcıya detay vermiyoruz (maskeleme korunuyor)
        await context.Response.WriteAsync(new ErrorDetails()
        {
            StatusCode = context.Response.StatusCode,
            Message = "Sunucu tarafında beklenmedik bir hata oluştu!"
        }.ToString());
    }

}