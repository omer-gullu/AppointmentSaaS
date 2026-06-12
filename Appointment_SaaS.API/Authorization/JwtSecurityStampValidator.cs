using Appointment_SaaS.Core.Entities;

namespace Appointment_SaaS.API.Authorization;

/// <summary>
/// JWT sonrası SecurityStamp doğrulaması (fail-closed).
/// </summary>
public static class JwtSecurityStampValidator
{
    public static bool IsValid(string? userIdClaim, string? stampClaim, AppUser? user)
    {
        if (!int.TryParse(userIdClaim, out _))
            return false;

        if (string.IsNullOrEmpty(stampClaim))
            return false;

        if (user == null)
            return false;

        return WebhookTokenComparer.Matches(stampClaim, user.SecurityStamp ?? string.Empty);
    }
}
