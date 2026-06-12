using System.Text.Json;

namespace Appointment_SaaS.WebUI.Diagnostics;

public static class PerfProbeLog
{
    private static readonly object Gate = new();
    private static string _logPath = Path.Combine(Directory.GetCurrentDirectory(), "debug-4e7483.log");

    public static void Configure(string logPath) => _logPath = logPath;

    public static void Write(string hypothesisId, string location, string message, object data, string runId = "probe")
    {
        try
        {
            var payload = new
            {
                sessionId = "4e7483",
                runId,
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            lock (Gate)
            {
                File.AppendAllText(_logPath, JsonSerializer.Serialize(payload) + Environment.NewLine);
            }
        }
        catch
        {
            // ignore probe failures
        }
    }
}
