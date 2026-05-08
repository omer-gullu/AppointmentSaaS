using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Appointment_SaaS.API.Middleware;

public class WebhookAuthenticationOptions : AuthenticationSchemeOptions { }

public class WebhookAuthenticationHandler : AuthenticationHandler<WebhookAuthenticationOptions>
{
    private readonly IConfiguration _configuration;

    public WebhookAuthenticationHandler(
        IOptionsMonitor<WebhookAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expectedToken = _configuration["WebhookSecurity:N8nAuthToken"];
        var providedToken = Request.Headers["X-Auth-Token"].FirstOrDefault();

        if (string.IsNullOrEmpty(providedToken) || providedToken != expectedToken)
            return Task.FromResult(AuthenticateResult.Fail("Geçersiz token."));

        var claims = new[] { new Claim(ClaimTypes.Name, "n8n-webhook") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}