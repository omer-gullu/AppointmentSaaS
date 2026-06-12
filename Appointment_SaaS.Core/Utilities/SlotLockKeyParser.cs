namespace Appointment_SaaS.Core.Utilities;

public static class SlotLockKeyParser
{
    public static bool TryGetTenantId(string lockKey, out int tenantId)
    {
        tenantId = 0;
        if (string.IsNullOrWhiteSpace(lockKey))
            return false;

        var parts = lockKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !parts[0].Equals("slot_lock", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(parts[1], out tenantId) && tenantId > 0;
    }
}
