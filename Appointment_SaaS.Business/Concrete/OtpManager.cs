using Appointment_SaaS.Business.Abstract;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Appointment_SaaS.Business.Concrete
{
    public class OtpManager : IOtpService
    {
        private readonly IMemoryCache _memoryCache;

        // Brute-force: kaç yanlış deneme yapıldığını tutan cache key prefix'i
        private const string FailPrefix = "OTP_FAIL_";
        private const int MaxAttempts = 3;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan OtpExpiry = TimeSpan.FromMinutes(3);

        public OtpManager(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        /// <summary>
        /// Kriptografik olarak güvenli 6 haneli OTP üretir ve cache'e kaydeder.
        /// RandomNumberGenerator kullanılır — new Random() tahmin edilebilir olduğundan güvensizdir.
        /// </summary>
        public string GenerateOtp(string phoneNumber)
        {
            // Yeni OTP üretilince önceki hatalı deneme sayacını sıfırla
            _memoryCache.Remove(FailPrefix + phoneNumber);

            // Kriptografik rastgele sayı: 100000–999999 arası
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            _memoryCache.Set("OTP_" + phoneNumber, code, OtpExpiry);

            return code;
        }

        /// <summary>
        /// OTP doğrular. 3 yanlış denemede hesap 15 dakika kilitlenir.
        /// Doğru kod girilirse cache'den silinir (tek kullanımlık).
        /// </summary>
        /// <exception cref="InvalidOperationException">Hesap kilitliyse fırlatılır.</exception>
        public bool VerifyOtp(string phoneNumber, string code)
        {
            var failKey = FailPrefix + phoneNumber;
            var otpKey = "OTP_" + phoneNumber;

            // Kilit kontrolü
            if (_memoryCache.TryGetValue(failKey, out int failCount) && failCount >= MaxAttempts)
            {
                throw new InvalidOperationException(
                    $"Çok fazla hatalı deneme. Lütfen {LockoutDuration.TotalMinutes} dakika sonra tekrar deneyin.");
            }

            if (_memoryCache.TryGetValue(otpKey, out string? storedCode) && storedCode == code)
            {
                // Doğru — her iki cache kaydını da temizle
                _memoryCache.Remove(otpKey);
                _memoryCache.Remove(failKey);
                return true;
            }

            // Yanlış deneme — sayacı artır
            var newCount = (failCount) + 1;
            _memoryCache.Set(failKey, newCount, LockoutDuration);

            return false;
        }
    }
}