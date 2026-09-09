namespace Appointment_SaaS.Core.Utilities.Security;

/// <summary>
/// Loglara düşen token / secret / OAuth gövdesi gibi hassas değerleri maskeler.
/// Amaç: log dosyalarına tam token/secret yazılmasını engellemek (PII / secret sızıntısı).
/// </summary>
public static class SensitiveDataMasker
{
    /// <summary>
    /// Bir token/secret'ı maskeler: yalnızca son 4 karakteri gösterir, gerisini gizler.
    /// Örn. "abcd1234efgh" → "***efgh" (uzunluk 12). Kısa/boş değerler tamamen gizlenir.
    /// </summary>
    public static string MaskToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "(boş)";

        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
            return "***";

        return $"***{trimmed[^4..]} (len={trimmed.Length})";
    }

    /// <summary>
    /// HTTP gövdesini loglamak için güvenli hale getirir: token/secret içerebilecek gövdeleri
    /// tamamen yazmak yerine yalnızca uzunluğunu bildirir.
    /// </summary>
    public static string SummarizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "(boş)";

        return $"(gövde gizlendi, len={body.Trim().Length})";
    }
}
