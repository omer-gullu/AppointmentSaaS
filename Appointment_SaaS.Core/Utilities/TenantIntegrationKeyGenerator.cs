using System.Security.Cryptography;

namespace Appointment_SaaS.Core.Utilities;

/// <summary>İşletme n8n entegrasyon anahtarı — tahmin edilemez uzunluk.</summary>
public static class TenantIntegrationKeyGenerator
{
    public static string Create()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
