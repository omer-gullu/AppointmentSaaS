using System.Globalization;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Model.V2.Subscription;
using Iyzipay.Request;
using Iyzipay.Request.V2.Subscription;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Appointment_SaaS.Business.Concrete;

public class IyzicoPaymentManager : IIyzicoPaymentService
{
    private readonly IyzicoSettings _settings;
    private readonly ILogger<IyzicoPaymentManager> _logger;

    public IyzicoPaymentManager(IOptions<IyzicoSettings> options, ILogger<IyzicoPaymentManager> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<IyzicoSubscriptionInitResult> InitializeSubscriptionAsync(
        Tenant tenant,
        string customerEmail,
        string customerFullName,
        string customerPhone,
        string customerIdentityNumber,
        IyzicoCardInput card,
        string planType,
        string billingCycle,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            throw new InvalidOperationException("Iyzico ödeme sistemi devre dışı (IyzicoSettings:Enabled=false).");

        if (string.IsNullOrWhiteSpace(_settings.ApiKey) ||
            string.IsNullOrWhiteSpace(_settings.SecretKey) ||
            string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            throw new InvalidOperationException("IyzicoSettings eksik (ApiKey/SecretKey/BaseUrl).");
        }

        var pricingPlanRefCode = GetPricingPlanReferenceCode(planType, billingCycle);

        if (string.IsNullOrWhiteSpace(pricingPlanRefCode))
        {
            throw new InvalidOperationException($"IyzicoSettings: {planType}-{billingCycle} için kod eksik.");
        }

        var options = new Iyzipay.Options
        {
            ApiKey = _settings.ApiKey,
            SecretKey = _settings.SecretKey,
            BaseUrl = _settings.BaseUrl
        };

        var conversationId = $"tenant_{tenant.TenantID}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        // 1) Card saklama (kartı DB'ye yazmıyoruz, sadece token)
        var cardRequest = new CreateCardRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = conversationId,
            Email = customerEmail,
            ExternalId = $"tenant_{tenant.TenantID}",
            Card = new CardInformation
            {
                CardAlias = "default",
                CardHolderName = card.CardHolderName,
                CardNumber = card.CardNumber,
                ExpireMonth = card.ExpireMonth,
                ExpireYear = card.ExpireYear
            }
        };

        var cardResponse = await Card.Create(cardRequest, options);
        if (cardResponse == null || cardResponse.Status != "success")
        {
            var err = cardResponse?.ErrorMessage ?? "Iyzico kart saklama başarısız.";
            _logger.LogWarning("Iyzico Card.Create failed. TenantId={TenantId} Error={Error}", tenant.TenantID, err);
            throw new InvalidOperationException(err);
        }

        var userKey = cardResponse.CardUserKey;
        var cardToken = cardResponse.CardToken;

        if (string.IsNullOrWhiteSpace(userKey) || string.IsNullOrWhiteSpace(cardToken))
        {
            throw new InvalidOperationException("Iyzico cardToken / cardUserKey alınamadı.");
        }

        // 2) Subscription başlat (trial plan üzerinde tanımlı olmalı)
        var subRequest = new SubscriptionInitializeRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = conversationId,
            PricingPlanReferenceCode = pricingPlanRefCode,
            Customer = new CheckoutFormCustomer
            {
                Name = SafeFirstName(customerFullName),
                Surname = SafeLastName(customerFullName),
                Email = customerEmail,
                GsmNumber = NormalizePhone(customerPhone),
                IdentityNumber = customerIdentityNumber,
                BillingAddress = new Address
                {
                    ContactName = customerFullName,
                    City = "Istanbul",
                    Country = "Turkey",
                    ZipCode = "34000",
                    Description = "Adres belirtilmedi"
                },
                ShippingAddress = new Address
                {
                    ContactName = customerFullName,
                    City = "Istanbul",
                    Country = "Turkey",
                    ZipCode = "34000",
                    Description = "Adres belirtilmedi"
                }
            },
            PaymentCard = new CardInfo
            {
                CardToken = cardToken,
                CardHolderName = card.CardHolderName,
                RegisterConsumerCard = false
            }
        };

        var subResponse = Subscription.Initialize(subRequest, options);
        if (subResponse == null || subResponse.Status != "success")
        {
            var err = subResponse?.ErrorMessage ?? "Iyzico subscription initialize başarısız.";
            _logger.LogWarning("Iyzico Subscription.Initialize failed. TenantId={TenantId} Error={Error}", tenant.TenantID, err);
            throw new InvalidOperationException(err);
        }

        var subscriptionReferenceCode = subResponse.Data?.ReferenceCode;

        return new IyzicoSubscriptionInitResult(
            IyzicoUserKey: userKey,
            IyzicoCardToken: cardToken,
            SubscriptionReferenceCode: subscriptionReferenceCode
        );
    }

    public async Task CancelSubscriptionAsync(string subscriptionReferenceCode, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            throw new InvalidOperationException("Iyzico ödeme sistemi devre dışı (IyzicoSettings:Enabled=false).");

        if (string.IsNullOrWhiteSpace(subscriptionReferenceCode))
            throw new ArgumentException("subscriptionReferenceCode boş olamaz.", nameof(subscriptionReferenceCode));

        var options = new Iyzipay.Options
        {
            ApiKey = _settings.ApiKey,
            SecretKey = _settings.SecretKey,
            BaseUrl = _settings.BaseUrl
        };

        var req = new CancelSubscriptionRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = $"cancel_{DateTime.UtcNow:yyyyMMddHHmmss}",
            SubscriptionReferenceCode = subscriptionReferenceCode
        };

        var res = Subscription.Cancel(req, options);
        if (res == null || res.Status != "success")
        {
            var err = res?.ErrorMessage ?? "Iyzico subscription cancel başarısız.";
            _logger.LogWarning("Iyzico Subscription.Cancel failed. Ref={Ref} Error={Error}", subscriptionReferenceCode, err);
            throw new InvalidOperationException(err);
        }

        await Task.CompletedTask;
    }

    private static string NormalizePhone(string phone)
    {
        phone = (phone ?? "").Trim();
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90") && digits.Length >= 12) return "+" + digits;
        if (digits.StartsWith("0") && digits.Length >= 11) digits = digits[1..];
        return digits.StartsWith("5") ? "+90" + digits : "+" + digits;
    }

    private static string SafeFirstName(string fullName)
    {
        fullName = (fullName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fullName)) return "Musteri";
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "Musteri";
    }

    private static string SafeLastName(string fullName)
    {
        fullName = (fullName ?? "").Trim();
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1) return "SaaS";
        return string.Join(" ", parts.Skip(1));
    }

    private string GetPricingPlanReferenceCode(string planType, string billingCycle)
    {
        if (string.Equals(planType, "trial", StringComparison.OrdinalIgnoreCase))
            return _settings.TrialPlanCode;

        var key = $"{planType}_{billingCycle}".ToLower();
        return key switch {
            "starter_monthly" => _settings.StarterMonthlyPlanCode,
            "starter_yearly" => _settings.StarterYearlyPlanCode,
            "business_monthly" => _settings.BusinessMonthlyPlanCode,
            "business_yearly" => _settings.BusinessYearlyPlanCode,
            "pro_monthly" => _settings.ProMonthlyPlanCode,
            "pro_yearly" => _settings.ProYearlyPlanCode,
            _ => throw new InvalidOperationException($"Geçersiz plan kombinasyonu: {planType} - {billingCycle}")
        };
    }
}

