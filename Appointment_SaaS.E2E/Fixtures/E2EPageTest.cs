using Appointment_SaaS.E2E.Configuration;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Appointment_SaaS.E2E.Fixtures;

/// <summary>
/// Tüm E2E testleri için ortak Playwright ayarları (base URL, HTTPS, timeout).
/// </summary>
public abstract class E2EPageTest : PageTest
{
    protected static E2EConfiguration Config { get; } = E2EConfiguration.Load();

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = Config.WebUiBaseUrl,
        IgnoreHTTPSErrors = Config.IgnoreHttpsErrors
    };

    protected async Task GotoAsync(string path)
    {
        Page.SetDefaultTimeout(Config.DefaultTimeoutMs);
        await Page.GotoAsync(path);
    }
}
