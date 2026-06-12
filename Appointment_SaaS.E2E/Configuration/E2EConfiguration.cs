using Microsoft.Extensions.Configuration;

namespace Appointment_SaaS.E2E.Configuration;

public sealed class E2EConfiguration
{
    public string WebUiBaseUrl { get; set; } = "https://localhost:7140";
    public string ApiBaseUrl { get; set; } = "http://localhost:5294";
    public bool IgnoreHttpsErrors { get; set; } = true;
    public int DefaultTimeoutMs { get; set; } = 30_000;

    public static E2EConfiguration Load()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var section = config.GetSection("E2E");
        var settings = new E2EConfiguration
        {
            WebUiBaseUrl = section["WebUiBaseUrl"] ?? "https://localhost:7140",
            ApiBaseUrl = section["ApiBaseUrl"] ?? "http://localhost:5294",
            IgnoreHttpsErrors = !bool.TryParse(section["IgnoreHttpsErrors"], out var ignore) || ignore,
            DefaultTimeoutMs = int.TryParse(section["DefaultTimeoutMs"], out var ms) ? ms : 30_000
        };

        var envBase = Environment.GetEnvironmentVariable("PLAYWRIGHT_BASE_URL");
        if (!string.IsNullOrWhiteSpace(envBase))
            settings.WebUiBaseUrl = envBase.TrimEnd('/');

        return settings;
    }
}
