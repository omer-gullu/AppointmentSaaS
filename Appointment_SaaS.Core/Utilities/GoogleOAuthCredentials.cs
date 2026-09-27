using Microsoft.Extensions.Configuration;

namespace Appointment_SaaS.Core.Utilities;

public static class GoogleOAuthCredentials
{
    public static bool TryGet(IConfiguration configuration, out string clientId, out string clientSecret)
    {
        clientId = configuration["Google:ClientId"]?.Trim() ?? "";
        clientSecret = configuration["Google:ClientSecret"]?.Trim() ?? "";
        return !IsUnusable(clientId) && !IsUnusable(clientSecret);
    }

    public static bool IsUnusable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        return value.Contains("FROM_ENV", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            || value.Contains("GOOGLE_CLIENT_SECRET_FROM_ENV", StringComparison.OrdinalIgnoreCase);
    }

    public static string DescribeRefreshFailure(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return "Google token yenilemesi başarısız.";
        if (responseBody.Contains("invalid_client", StringComparison.OrdinalIgnoreCase))
            return "Google istemci anahtarı API tarafında uyuşmuyor. WebUI ile aynı Google__ClientSecret kullanılmalı.";
        if (responseBody.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
            return "Google yenileme anahtarı geçersiz. Personeli Google ile yeniden bağlayın.";
        return "Google token yenilemesi başarısız.";
    }
}
