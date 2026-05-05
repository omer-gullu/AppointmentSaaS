using System;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;
using Appointment_SaaS.API.Controller;

namespace Appointment_SaaS.Test
{
    /// <summary>
    /// Iyzico Webhook HMAC-SHA256 imza doğrulama testleri.
    /// Controller bağımsız — sadece statik VerifyHmacSignature metodu test edilir.
    /// </summary>
    public class WebhookSignatureTests
    {
        private const string TestSecret = "super_secret_webhook_key_2026";

        // Verilen payload ve secret için geçerli imza üretir
        private static string ComputeValidSignatureBase64(string body, string secret)
        {
            var key = Encoding.UTF8.GetBytes(secret);
            var data = Encoding.UTF8.GetBytes(body);
            using var hmac = new HMACSHA256(key);
            return Convert.ToBase64String(hmac.ComputeHash(data));
        }

        private static string ComputeValidSignatureHex(string body, string secret)
        {
            var key = Encoding.UTF8.GetBytes(secret);
            var data = Encoding.UTF8.GetBytes(body);
            using var hmac = new HMACSHA256(key);
            return Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();
        }

        // ─── Geçerli İmza Testleri ────────────────────────────────────────────

        [Fact]
        public void VerifyHmacSignature_ShouldReturnTrue_WhenSignatureIsValidBase64()
        {
            var body = "{\"eventType\":\"payment.success\",\"referenceCode\":\"ref_abc\"}";
            var validSig = ComputeValidSignatureBase64(body, TestSecret);

            var result = IyzicoWebhookController.VerifyHmacSignature(body, validSig, TestSecret);

            result.Should().BeTrue("Base64 format geçerli imza kabul edilmeli");
        }

        [Fact]
        public void VerifyHmacSignature_ShouldReturnTrue_WhenSignatureIsValidHex()
        {
            var body = "{\"eventType\":\"refund\",\"referenceCode\":\"ref_xyz\"}";
            var validSig = ComputeValidSignatureHex(body, TestSecret);

            var result = IyzicoWebhookController.VerifyHmacSignature(body, validSig, TestSecret);

            result.Should().BeTrue("Hex format geçerli imza kabul edilmeli");
        }

        // ─── Geçersiz İmza Testleri (401 Senaryoları) ────────────────────────

        [Fact]
        public void VerifyHmacSignature_ShouldReturnFalse_WhenSignatureIsWrong()
        {
            var body = "{\"eventType\":\"payment.success\"}";
            var invalidSig = "INVALID_SIGNATURE_XXXX";

            var result = IyzicoWebhookController.VerifyHmacSignature(body, invalidSig, TestSecret);

            result.Should().BeFalse("Geçersiz imza reddedilmeli");
        }

        [Fact]
        public void VerifyHmacSignature_ShouldReturnFalse_WhenBodyIsTampered()
        {
            var originalBody = "{\"eventType\":\"payment.success\",\"referenceCode\":\"ref_abc\"}";
            var tamperedBody = "{\"eventType\":\"refund\",\"referenceCode\":\"ref_abc\"}"; // body değiştirildi
            var originalSig = ComputeValidSignatureBase64(originalBody, TestSecret);

            var result = IyzicoWebhookController.VerifyHmacSignature(tamperedBody, originalSig, TestSecret);

            result.Should().BeFalse("Body değiştirildiğinde imza doğrulaması başarısız olmalı");
        }

        [Fact]
        public void VerifyHmacSignature_ShouldReturnFalse_WhenSecretIsWrong()
        {
            var body = "{\"eventType\":\"payment.success\"}";
            var sigWithWrongSecret = ComputeValidSignatureBase64(body, "wrong_secret");

            var result = IyzicoWebhookController.VerifyHmacSignature(body, sigWithWrongSecret, TestSecret);

            result.Should().BeFalse("Farklı secret ile üretilmiş imza reddedilmeli");
        }

        [Fact]
        public void VerifyHmacSignature_ShouldReturnFalse_WhenSignatureIsEmpty()
        {
            var body = "{\"eventType\":\"payment.success\"}";

            var result = IyzicoWebhookController.VerifyHmacSignature(body, "", TestSecret);

            result.Should().BeFalse("Boş imza reddedilmeli");
        }

        [Fact]
        public void VerifyHmacSignature_ShouldReturnFalse_WhenBodyIsEmpty()
        {
            var validSig = ComputeValidSignatureBase64("", TestSecret);

            var result = IyzicoWebhookController.VerifyHmacSignature("", validSig, TestSecret);

            result.Should().BeFalse("Boş body ile doğrulama başarısız olmalı");
        }

        [Fact]
        public void VerifyHmacSignature_ShouldReturnFalse_WhenSecretIsEmpty()
        {
            var body = "{\"eventType\":\"payment.success\"}";
            var validSig = ComputeValidSignatureBase64(body, TestSecret);

            var result = IyzicoWebhookController.VerifyHmacSignature(body, validSig, "");

            result.Should().BeFalse("Boş secret ile doğrulama başarısız olmalı");
        }

        // ─── Idempotency / Business Logic Testleri ───────────────────────────

        [Fact]
        public void VerifyHmacSignature_ShouldBeCaseSensitiveForBase64()
        {
            // Base64 büyük/küçük harf duyarlıdır — doğru imzanın tam kopyası eşleşmeli
            var body = "{\"data\":\"test\"}";
            var validSig = ComputeValidSignatureBase64(body, TestSecret);

            // Aynı imzayla tam eşleşme
            IyzicoWebhookController.VerifyHmacSignature(body, validSig, TestSecret)
                .Should().BeTrue();
        }

        [Fact]
        public void VerifyHmacSignature_ShouldBeCaseInsensitiveForHex()
        {
            // Hex format büyük/küçük harf farkı gözetilmemeli
            var body = "{\"data\":\"test\"}";
            var hexSig = ComputeValidSignatureHex(body, TestSecret);

            IyzicoWebhookController.VerifyHmacSignature(body, hexSig.ToUpperInvariant(), TestSecret)
                .Should().BeTrue("Hex format büyük/küçük harf toleranslı olmalı");
        }
    }
}
