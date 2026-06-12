using System.Text.RegularExpressions;

namespace Appointment_SaaS.API.Authorization;

/// <summary>
/// n8n / webhook için X-Auth-Token gerektiren path kuralları (test edilebilir).
/// </summary>
public static class WebhookProtectedPathEvaluator
{
    private static readonly string[] ExcludedPathPrefixes =
    {
        "/api/iyzico/webhook"
    };

    private static readonly string[] ProtectedApiPathPrefixes =
    {
        "/api/tenants/getcontextbyinstance",
        "/api/tenants/getgoogleaccesstoken",
        "/api/appointments/customer/",
        "/api/appointments/tomorrow",
        "/api/appointments/reminders/pending",
        "/api/appointments/available-slots",
        "/api/appointments/my-active-appointments",
        "/api/appusers/staff/",
        "/api/services/businessphone/",
        "/api/services/tenant/",
        "/api/whatsappblockedphones/check"
    };

    private static readonly Regex[] ProtectedWebhookPostPatterns =
    {
        new(@"^/api/appointments$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/appointments/\d+/google-event$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/appointments/lock$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/appointments/unlock$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/auditlogs/workflow-error$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/appointments/\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/feedbacks$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/whatsappblockedphones/opt-out$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/appointments/reminders/run$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    /// <summary>n8n: DELETE /api/appointments/{id} — webhook ile silme.</summary>
    private static readonly Regex[] ProtectedWebhookDeletePatterns =
    {
        new(@"^/api/appointments/\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    /// <summary>n8n: PUT /api/appointments/{id} — webhook ile randevu güncelleme.</summary>
    private static readonly Regex[] ProtectedWebhookPutPatterns =
    {
        new(@"^/api/appointments/\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    /// <summary>GET /api/services/123 — tek hizmet detayı (fiyat/süre).</summary>
    private static readonly Regex ProtectedServiceByIdGet =
        new(@"^/api/services/\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

  private static readonly string[] SystemOnlyPathPrefixes =
    {
        "/api/appointments/reminders/pending",
        "/api/appointments/reminders/run"
    };

    /// <summary>n8n bootstrap: işletmeyi keşfetmek için sistem token ile çağrılabilir.</summary>
    private static readonly string[] SystemBootstrapPathPrefixes =
    {
        "/api/tenants/getcontextbyinstance"
    };

    public static bool AllowsSystemBootstrap(string path, string method) =>
        string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
        && SystemBootstrapPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    public static bool IsExcluded(string path) =>
        ExcludedPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>Çok kiracılı cron — yalnızca N8nAuthToken (sistem).</summary>
    public static bool IsSystemOnlyPath(string path, string method)
    {
        if (!SystemOnlyPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (path.Contains("reminders/run", StringComparison.OrdinalIgnoreCase))
            return string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);

        if (path.Contains("reminders/pending", StringComparison.OrdinalIgnoreCase))
            return string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);

        return false;
    }

    public static bool RequiresWebhookToken(string path, string method)
    {
        if (IsExcluded(path))
            return false;

        if (IsSystemOnlyPath(path, method))
            return true;

        if (ProtectedApiPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            && ProtectedServiceByIdGet.IsMatch(path))
            return true;

        if ((string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
             || string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase))
            && ProtectedWebhookPostPatterns.Any(r => r.IsMatch(path)))
            return true;

        if (string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase)
            && ProtectedWebhookDeletePatterns.Any(r => r.IsMatch(path)))
            return true;

        if (string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase)
            && ProtectedWebhookPutPatterns.Any(r => r.IsMatch(path)))
            return true;

        return false;
    }
}
