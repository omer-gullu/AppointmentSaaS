using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Appointment_SaaS.Business.Abstract;
using Microsoft.Extensions.Configuration;

namespace Appointment_SaaS.Business.Concrete;

public class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration configuration)
    {
        var keyString = configuration["EncryptionSettings:AesKey"] ?? throw new ArgumentNullException("EncryptionSettings:AesKey eksik.");
        if (keyString.Length != 32) throw new ArgumentException("Güvenlik Key, AES-256 için 32 karakter olmalıdır.");
        
        _key = Encoding.UTF8.GetBytes(keyString);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var memoryStream = new MemoryStream();
        using var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
        using (var streamWriter = new StreamWriter(cryptoStream))
        {
            streamWriter.Write(plainText);
        }

        return Convert.ToBase64String(aes.IV.Concat(memoryStream.ToArray()).ToArray());
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        byte[] fullCipher = Convert.FromBase64String(cipherText);
        byte[] iv = fullCipher.Take(16).ToArray();
        byte[] cipher = fullCipher.Skip(16).ToArray();

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var memoryStream = new MemoryStream(cipher);
        using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
        using var streamReader = new StreamReader(cryptoStream);
        
        return streamReader.ReadToEnd();
    }
}
