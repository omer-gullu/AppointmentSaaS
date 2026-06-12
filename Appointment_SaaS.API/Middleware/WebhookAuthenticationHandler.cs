using Appointment_SaaS.API.Authorization;

using Microsoft.AspNetCore.Authentication;

using Microsoft.Extensions.Options;

using System.Security.Claims;

using System.Text.Encodings.Web;



namespace Appointment_SaaS.API.Middleware;



public class WebhookAuthenticationOptions : AuthenticationSchemeOptions { }



public class WebhookAuthenticationHandler : AuthenticationHandler<WebhookAuthenticationOptions>

{

    private readonly WebhookAuthValidator _validator;



    public WebhookAuthenticationHandler(

        IOptionsMonitor<WebhookAuthenticationOptions> options,

        ILoggerFactory logger,

        UrlEncoder encoder,

        WebhookAuthValidator validator)

        : base(options, logger, encoder)

    {

        _validator = validator;

    }



    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()

    {

        var path = Request.Path.Value ?? "";

        var method = Request.Method;



        if (!WebhookProtectedPathEvaluator.RequiresWebhookToken(path, method))

            return AuthenticateResult.NoResult();



        var result = await _validator.EvaluateAsync(Context, path, method);



        switch (result.Kind)

        {

            case WebhookAuthResultKind.AllowTenant:

                Context.Items[WebhookContextKeys.TenantId] = result.TenantId;

                return Success("n8n-webhook-tenant");

            case WebhookAuthResultKind.AllowSystem:

                Context.Items[WebhookContextKeys.IsSystemWebhook] = true;

                return Success("n8n-webhook-system");

            case WebhookAuthResultKind.AllowJwt:

            case WebhookAuthResultKind.NotApplicable:

                return AuthenticateResult.NoResult();

            default:

                return AuthenticateResult.Fail(result.Message ?? "Geçersiz webhook kimlik bilgisi.");

        }

    }



    private AuthenticateResult Success(string name)

    {

        var claims = new[] { new Claim(ClaimTypes.Name, name) };

        var identity = new ClaimsIdentity(claims, Scheme.Name);

        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);

    }

}

