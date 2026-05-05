using System.Security.Cryptography;
using System.Text;

namespace Appointment_SaaS.Core.Utilities.Security;

/// <summary>
/// AES-256-CBC şifreleme/çözme yardımcı sınıfı.
/// Evolution API Instance Key'leri gibi hassas verileri veritabanında şifreli saklamak için kullanılır.
/// 
/// Kullanım:
///   var encrypted = AesEncryptionHelper.Encrypt("gizli-api-key", aesKeyFromConfig);
///   var decrypted = AesEncryptionHelper.Decrypt(encrypted, aesKeyFromConfig);
/// 
/// aesKey: Tam 32 karakter (256-bit) uzunluğunda olmalıdır.
/// </summary>
public static class AesEncryptionHelper
{
    /// <summary>
    /// Verilen düz metni AES-256-CBC ile şifreler.
    /// Sonuç: Base64(IV + CipherText) formatında döner.
    /// </summary>
    public static string Encrypt(string plainText, string key)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentNullException(nameof(plainText));
        if (string.IsNullOrEmpty(key) || key.Length != 32)
            throw new ArgumentException("AES anahtarı tam 32 karakter (256-bit) uzunluğunda olmalıdır.", nameof(key));

        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV(); // Her şifrelemede rastgele IV üretir (güvenlik için kritik)

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // IV + CipherText birleştir ve Base64'e çevir
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// AES-256-CBC ile şifrelenmiş Base64 metni çözer.
    /// Giriş formatı: Base64(IV + CipherText)
    /// </summary>
    public static string Decrypt(string cipherTextBase64, string key)
    {
        if (string.IsNullOrEmpty(cipherTextBase64))
            throw new ArgumentNullException(nameof(cipherTextBase64));
        if (string.IsNullOrEmpty(key) || key.Length != 32)
            throw new ArgumentException("AES anahtarı tam 32 karakter (256-bit) uzunluğunda olmalıdır.", nameof(key));

        var fullCipher = Convert.FromBase64String(cipherTextBase64);

        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // İlk 16 byte IV, geri kalanı şifreli veri
        var iv = new byte[16];
        var cipherBytes = new byte[fullCipher.Length - 16];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
        Buffer.BlockCopy(fullCipher, 16, cipherBytes, 0, cipherBytes.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
