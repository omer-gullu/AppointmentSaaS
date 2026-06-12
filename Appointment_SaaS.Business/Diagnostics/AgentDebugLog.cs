using System.Text;
using System.Text.Json;

namespace Appointment_SaaS.Business.Diagnostics;

/// <summary>Debug session NDJSON (session cacf81). Secrets/PII yazılmaz.</summary>
public static class AgentDebugLog
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private const string IngestUrl = "http://127.0.0.1:7641/ingest/b1662654-1d28-4688-a523-e377773c3b1a";

    public static void Write(string hypothesisId, string location, string message, object? data = null, string runId = "pre")
    {
        // #region agent log
        var payload = new Dictionary<string, object?>
        {
            ["sessionId"] = "cacf81",
            ["hypothesisId"] = hypothesisId,
            ["location"] = location,
            ["message"] = message,
            ["data"] = data,
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["runId"] = runId
        };
        var line = JsonSerializer.Serialize(payload) + Environment.NewLine;

        foreach (var path in ResolveLogPaths())
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.AppendAllText(path, line);
            }
            catch
            {
                /* try next path */
            }
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, IngestUrl);
            req.Headers.Add("X-Debug-Session-Id", "cacf81");
            req.Content = new StringContent(line.TrimEnd(), Encoding.UTF8, "application/json");
            _ = Http.SendAsync(req);
        }
        catch
        {
            /* ignore */
        }
        // #endregion
    }

    private static List<string> ResolveLogPaths()
    {
        var fileName = "debug-cacf81.log";
        var paths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(Directory.GetCurrentDirectory(), fileName),
            Path.Combine(Path.GetTempPath(), fileName),
            @"c:\Users\pc\OneDrive\Masaüstü\AppointmentSaaS\debug-cacf81.log"
        };

        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 10; i++)
        {
            if (File.Exists(Path.Combine(dir, "Appointment_SaaS.sln")))
            {
                paths.Insert(0, Path.Combine(dir, fileName));
                break;
            }
            var parent = Directory.GetParent(dir)?.FullName;
            if (string.IsNullOrEmpty(parent)) break;
            dir = parent;
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
