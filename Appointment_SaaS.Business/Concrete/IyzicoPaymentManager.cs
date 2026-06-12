using System.Globalization;
using System.Collections.Generic;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Diagnostics;
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

    public async Task<IyzicoCheckoutFormInitResult> InitializeSubscriptionCheckoutFormAsync(
        Tenant tenant,
        string customerEmail,
        string customerFullName,
        string customerPhone,
        string customerIdentityNumber,
        string planType,
        string billingCycle,
        string callbackUrl,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // async warning fix
        if (!_settings.Enabled)
            throw new InvalidOperationException("Iyzico ödeme sistemi devre dışı (IyzicoSettings:Enabled=false).");

        if (string.IsNullOrWhiteSpace(_settings.ApiKey) ||
            string.IsNullOrWhiteSpace(_settings.SecretKey) ||
            string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            throw new InvalidOperationException("IyzicoSettings eksik (ApiKey/SecretKey/BaseUrl).");
        }

        var pricingPlanRefCode = ResolvePricingPlanReferenceCode(planType, billingCycle);

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

        var addressLine = string.IsNullOrWhiteSpace(tenant.Address)
            ? "Adres belirtilmedi"
            : tenant.Address.Trim();

        var subRequest = new InitializeCheckoutFormRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = conversationId,
            PricingPlanReferenceCode = pricingPlanRefCode,
            CallbackUrl = callbackUrl,
            SubscriptionInitialStatus = "ACTIVE",
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
                    Country = "Türkiye",
                    ZipCode = "34000",
                    Description = addressLine
                },
                ShippingAddress = new Address
                {
                    ContactName = customerFullName,
                    City = "Istanbul",
                    Country = "Türkiye",
                    ZipCode = "34000",
                    Description = addressLine
                }
            }
        };

        // InitializeCheckoutFormRequest -> Subscription.InitializeCheckoutForm (V2)
        var subResponse = Iyzipay.Model.V2.Subscription.Subscription.InitializeCheckoutForm(subRequest, options);
        
        if (subResponse == null || subResponse.Status != "success")
        {
            var err = subResponse?.ErrorMessage ?? "Iyzico Subscription Checkout Form initialize başarısız.";
            _logger.LogWarning("Iyzico CheckoutFormInitialize.Create failed. TenantId={TenantId} Error={Error}", tenant.TenantID, err);
            throw new InvalidOperationException(err);
        }

        return new IyzicoCheckoutFormInitResult(
            CheckoutFormContent: subResponse.CheckoutFormContent,
            Token: subResponse.Token
        );
    }

    public async Task<IyzicoCheckoutFormInitResult> InitializeTrialCardValidationCheckoutAsync(
        Tenant tenant,
        string customerEmail,
        string customerFullName,
        string customerPhone,
        string customerIdentityNumber,
        string callbackUrl,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (!_settings.Enabled)
            throw new InvalidOperationException("Iyzico ödeme sistemi devre dışı (IyzicoSettings:Enabled=false).");

        if (string.IsNullOrWhiteSpace(_settings.ApiKey) ||
            string.IsNullOrWhiteSpace(_settings.SecretKey) ||
            string.IsNullOrWhiteSpace(_settings.BaseUrl))
            throw new InvalidOperationException("IyzicoSettings eksik (ApiKey/SecretKey/BaseUrl).");

        var amount = _settings.TrialCardValidationAmountTry <= 0 ? 1.00m : _settings.TrialCardValidationAmountTry;
        var priceStr = amount.ToString("F2", CultureInfo.InvariantCulture);

        var options = new Iyzipay.Options
        {
            ApiKey = _settings.ApiKey,
            SecretKey = _settings.SecretKey,
            BaseUrl = _settings.BaseUrl
        };

        var conversationId = $"trial_tenant_{tenant.TenantID}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var buyer = new Buyer
        {
            Id = $"BY_TRIAL_{tenant.TenantID}",
            Name = SafeFirstName(customerFullName),
            Surname = SafeLastName(customerFullName),
            GsmNumber = NormalizePhone(customerPhone),
            Email = string.IsNullOrWhiteSpace(customerEmail) ? $"trial{tenant.TenantID}@placeholder.local" : customerEmail,
            IdentityNumber = string.IsNullOrWhiteSpace(customerIdentityNumber) ? "11111111111" : customerIdentityNumber,
            RegistrationAddress = "Kayit",
            Ip = _settings.RefundCallerIp,
            City = "Istanbul",
            Country = "Turkey",
            ZipCode = "34000",
            LastLoginDate = nowStr,
            RegistrationDate = nowStr
        };

        var addr = new Address
        {
            ContactName = customerFullName,
            City = "Istanbul",
            Country = "Turkey",
            ZipCode = "34000",
            Description = "Kayit"
        };

        var basketItems = new List<BasketItem>
        {
            new BasketItem
            {
                Id = "trial-card-validation",
                Name = "Kart dogrulama",
                Category1 = "Genel",
                ItemType = BasketItemType.VIRTUAL.ToString(),
                Price = priceStr
            }
        };

        var request = new CreateCheckoutFormInitializeRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = conversationId,
            Price = priceStr,
            PaidPrice = priceStr,
            Currency = Currency.TRY.ToString(),
            BasketId = $"TRIAL_BASKET_{tenant.TenantID}_{DateTime.UtcNow:yyyyMMddHHmmss}",
            PaymentGroup = PaymentGroup.PRODUCT.ToString(),
            CallbackUrl = callbackUrl,
            Buyer = buyer,
            BillingAddress = addr,
            ShippingAddress = addr,
            BasketItems = basketItems
        };

        var init = CheckoutFormInitialize.Create(request, options);
        if (init == null || init.Status != "success")
        {
            var err = init?.ErrorMessage ?? "Iyzico trial checkout form başlatılamadı.";
            _logger.LogWarning("Iyzico trial CheckoutFormInitialize failed. TenantId={TenantId} Error={Error}", tenant.TenantID, err);
            throw new InvalidOperationException(err);
        }

        return new IyzicoCheckoutFormInitResult(
            CheckoutFormContent: init.CheckoutFormContent ?? "",
            Token: init.Token ?? "");
    }

    public async Task VerifyTrialCheckoutFormAndRefundAsync(string token, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (!_settings.Enabled)
            throw new InvalidOperationException("Iyzico ödeme sistemi devre dışı (IyzicoSettings:Enabled=false).");

        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("token boş olamaz.", nameof(token));

        var options = new Iyzipay.Options
        {
            ApiKey = _settings.ApiKey,
            SecretKey = _settings.SecretKey,
            BaseUrl = _settings.BaseUrl
        };

        var retrieveReq = new RetrieveCheckoutFormRequest
        {
            Locale = Locale.TR.ToString(),
            Token = token
        };

        var form = CheckoutForm.Retrieve(retrieveReq, options);
        if (form == null || form.Status != "success" || form.PaymentStatus != "SUCCESS")
        {
            var err = form?.ErrorMessage ?? "Trial ödeme onaylanamadı.";
            _logger.LogWarning("Iyzico trial CheckoutForm.Retrieve failed. Token={Token} Error={Error}", token, err);
            throw new InvalidOperationException(err);
        }

        if (form.PaymentItems == null || form.PaymentItems.Count == 0)
        {
            _logger.LogWarning("Trial checkout sonrası PaymentItems boş. Token={Token}", token);
            throw new InvalidOperationException("Ödeme kalemi bulunamadı; iade yapılamadı.");
        }

        var first = form.PaymentItems[0] as PaymentItem;
        if (first == null || string.IsNullOrWhiteSpace(first.PaymentTransactionId))
            throw new InvalidOperationException("PaymentTransactionId alınamadı.");

        var refundPrice = string.IsNullOrWhiteSpace(first.Price) ? form.PaidPrice : first.Price;
        if (string.IsNullOrWhiteSpace(refundPrice))
            refundPrice = _settings.TrialCardValidationAmountTry.ToString("F2", CultureInfo.InvariantCulture);

        var refundReq = new CreateRefundRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = $"trial_refund_{DateTime.UtcNow:yyyyMMddHHmmss}",
            PaymentTransactionId = first.PaymentTransactionId,
            Price = refundPrice,
            Currency = Currency.TRY.ToString(),
            Ip = _settings.RefundCallerIp,
            Reason = "other",
            Description = "Trial kart dogrulama iadesi"
        };

        var refund = Refund.Create(refundReq, options);
        if (refund == null || refund.Status != "success")
        {
            var err = refund?.ErrorMessage ?? "Trial iade başarısız.";
            _logger.LogError("Iyzico trial refund failed. Token={Token} Tx={Tx} Error={Error}", token, first.PaymentTransactionId, err);
            throw new InvalidOperationException(err);
        }

        _logger.LogInformation("Trial kart doğrulama tutarı iade edildi. PaymentTx={Tx}", first.PaymentTransactionId);
    }

    public async Task<IyzicoCheckoutVerifyResult> VerifyCheckoutFormAsync(
        string token,
        string? pricingPlanReferenceCode = null,
        string? excludeSubscriptionReferenceCode = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (!_settings.Enabled)
            throw new InvalidOperationException("Iyzico ödeme sistemi devre dışı (IyzicoSettings:Enabled=false).");

        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("token boş olamaz.", nameof(token));

        var options = CreateOptions();

        string? subscriptionRef = null;
        string? customerRef = null;

        if (!string.IsNullOrWhiteSpace(pricingPlanReferenceCode))
        {
            var resolved = await TryResolveSubscriptionByPricingPlanAsync(
                options,
                pricingPlanReferenceCode,
                excludeSubscriptionReferenceCode,
                preferSubscriptionReferenceCode: null,
                cancellationToken);
            subscriptionRef = resolved?.SubscriptionReferenceCode;
            customerRef = resolved?.CustomerReferenceCode;
        }

        if (string.IsNullOrWhiteSpace(subscriptionRef))
        {
            AgentDebugLog.Write("H-B", "IyzicoPaymentManager.VerifyCheckoutForm",
                "subscription_ref_unresolved", new
                {
                    hasPricingRef = !string.IsNullOrWhiteSpace(pricingPlanReferenceCode),
                    hadExclude = !string.IsNullOrWhiteSpace(excludeSubscriptionReferenceCode)
                }, "post-fix");
            throw new InvalidOperationException(
                "Ödeme alındı ancak İyzico abonelik referansı çözümlenemedi. Destek ile iletişime geçin.");
        }

        AgentDebugLog.Write("H3", "IyzicoPaymentManager.VerifyCheckoutForm", "resolved", new
        {
            hasPricingRef = !string.IsNullOrWhiteSpace(pricingPlanReferenceCode),
            subRefLen = subscriptionRef.Length,
            excludedPrevious = !string.IsNullOrWhiteSpace(excludeSubscriptionReferenceCode)
        });

        return new IyzicoCheckoutVerifyResult(token, subscriptionRef, customerRef);
    }

    public async Task UpgradeSubscriptionAsync(
        string subscriptionReferenceCode,
        string newPricingPlanReferenceCode,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (!_settings.Enabled)
            throw new InvalidOperationException("Iyzico ödeme sistemi devre dışı (IyzicoSettings:Enabled=false).");

        var options = CreateOptions();
        var req = new UpgradeSubscriptionRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = $"upgrade_{DateTime.UtcNow:yyyyMMddHHmmss}",
            SubscriptionReferenceCode = subscriptionReferenceCode,
            NewPricingPlanReferenceCode = newPricingPlanReferenceCode,
            UpgradePeriod = SubscriptionUpgradePeriod.NOW.ToString(),
            UseTrial = false,
            ResetRecurrenceCount = true
        };

        var res = Subscription.Upgrade(req, options);
        if (res == null || res.Status != "success")
        {
            var err = res?.ErrorMessage ?? "Iyzico subscription upgrade başarısız.";
            _logger.LogWarning("Iyzico Subscription.Upgrade failed. Ref={Ref} Error={Error}", subscriptionReferenceCode, err);
            throw new InvalidOperationException(err);
        }
    }

    public async Task<IyzicoSubscriptionDetailResult?> GetSubscriptionDetailAsync(
        string subscriptionReferenceCode,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(subscriptionReferenceCode))
            return null;

        var options = CreateOptions();
        var req = new RetrieveSubscriptionRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = $"detail_{DateTime.UtcNow:yyyyMMddHHmmss}",
            SubscriptionReferenceCode = subscriptionReferenceCode
        };

        var res = Subscription.Retrieve(req, options);
        if (res == null || res.Status != "success" || res.Data == null)
            return null;

        var sub = res.Data;
        DateTime? endDate = null;

        string endDateSource = "fallback_start";
        if (sub.SubscriptionOrders != null && sub.SubscriptionOrders.Count > 0)
        {
            var now = DateTime.Now;
            var periods = sub.SubscriptionOrders
                .Select(o => (Start: ParseIyzicoPeriod(o.StartPeriod), End: ParseIyzicoPeriod(o.EndPeriod)))
                .Where(p => p.End.HasValue)
                .ToList();

            var current = periods.FirstOrDefault(p => p.Start.HasValue && p.Start <= now && now < p.End);
            if (current.End.HasValue)
            {
                endDate = current.End;
                endDateSource = "current_period";
            }
            else
            {
                var nextEnd = periods
                    .Where(p => p.End!.Value >= now)
                    .OrderBy(p => p.End)
                    .Select(p => p.End)
                    .FirstOrDefault();
                if (nextEnd.HasValue)
                {
                    endDate = nextEnd;
                    endDateSource = "next_period";
                }
                else
                {
                    endDate = periods.Max(p => p.End);
                    endDateSource = "max_historical_order";
                }
            }
        }

        var start = FromIyzicoEpoch(sub.StartDate);
        if (!endDate.HasValue && start.HasValue)
        {
            endDate = IsYearlyPricingPlan(sub.PricingPlanReferenceCode)
                ? start.Value.AddYears(1)
                : start.Value.AddMonths(1);
            endDateSource = "computed_from_start";
        }

        AgentDebugLog.Write("H6", "IyzicoPaymentManager.GetSubscriptionDetail", "end_date_computed", new
        {
            subRefLen = (sub.ReferenceCode ?? subscriptionReferenceCode).Length,
            isYearly = IsYearlyPricingPlan(sub.PricingPlanReferenceCode),
            endDate = endDate?.ToString("o"),
            orderCount = sub.SubscriptionOrders?.Count ?? 0,
            endDateSource
        });

        return new IyzicoSubscriptionDetailResult(
            sub.ReferenceCode ?? subscriptionReferenceCode,
            sub.SubscriptionStatus,
            endDate,
            sub.PricingPlanReferenceCode);
    }

    public string GetPricingPlanReferenceCode(string planType, string billingCycle) =>
        ResolvePricingPlanReferenceCode(planType, billingCycle);

    public async Task<IyzicoSubscriptionDetailResult?> ResolveLatestActiveSubscriptionAsync(
        string pricingPlanReferenceCode,
        string? excludeSubscriptionReferenceCode = null,
        string? preferSubscriptionReferenceCode = null,
        CancellationToken cancellationToken = default)
    {
        var options = CreateOptions();
        var resolved = await TryResolveSubscriptionByPricingPlanAsync(
            options,
            pricingPlanReferenceCode,
            excludeSubscriptionReferenceCode,
            preferSubscriptionReferenceCode,
            cancellationToken);

        if (resolved == null || string.IsNullOrWhiteSpace(resolved.SubscriptionReferenceCode))
            return null;

        return await GetSubscriptionDetailAsync(resolved.SubscriptionReferenceCode, cancellationToken);
    }

    public async Task<IyzicoSubscriptionDetailResult?> ResolveLatestActiveByCustomerAsync(
        string customerReferenceCode,
        string? preferSubscriptionReferenceCode = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(customerReferenceCode))
            return null;

        if (!string.IsNullOrWhiteSpace(preferSubscriptionReferenceCode))
        {
            var preferred = await TryGetActiveDetailAsync(preferSubscriptionReferenceCode, cancellationToken);
            if (preferred != null)
            {
                AgentDebugLog.Write("H8", "IyzicoPaymentManager.ResolveLatestActiveByCustomer", "used_preferred_ref", new
                {
                    endDate = preferred.EndDate?.ToString("o"),
                    selection = "prefer_db_ref"
                }, "post-fix");
                return preferred;
            }
        }

        var options = CreateOptions();
        var searchReq = new SearchSubscriptionRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = $"cust_{DateTime.UtcNow:yyyyMMddHHmmss}",
            CustomerReferenceCode = customerReferenceCode,
            Page = 1,
            Count = 20
        };

        var searchRes = Subscription.Search(searchReq, options);
        var items = searchRes?.Data?.Items;
        if (searchRes == null || searchRes.Status != "success" || items == null || items.Count == 0)
            return null;

        foreach (var item in items.OrderByDescending(s => FromIyzicoEpoch(s.CreatedDate) ?? DateTime.MinValue))
        {
            if (string.IsNullOrWhiteSpace(item.ReferenceCode))
                continue;

            var detail = await TryGetActiveDetailAsync(item.ReferenceCode, cancellationToken);
            if (detail != null)
            {
                AgentDebugLog.Write("H8", "IyzicoPaymentManager.ResolveLatestActiveByCustomer", "customer_search_done", new
                {
                    itemCount = items.Count,
                    activeCandidates = items.Count(i => !string.IsNullOrWhiteSpace(i.ReferenceCode)),
                    selection = "newest_created",
                    endDate = detail.EndDate?.ToString("o"),
                    chosenRefLen = detail.SubscriptionReferenceCode.Length
                }, "post-fix");
                return detail;
            }
        }

        AgentDebugLog.Write("H8", "IyzicoPaymentManager.ResolveLatestActiveByCustomer", "customer_search_none", new
        {
            itemCount = items.Count
        }, "post-fix");

        return null;
    }

    private async Task<IyzicoSubscriptionDetailResult?> TryGetActiveDetailAsync(
        string subscriptionReferenceCode,
        CancellationToken cancellationToken)
    {
        var detail = await GetSubscriptionDetailAsync(subscriptionReferenceCode, cancellationToken);
        if (detail?.EndDate == null || detail.EndDate < DateTime.Now)
            return null;

        var status = detail.SubscriptionStatus ?? string.Empty;
        if (!string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "UPGRADED", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return detail;
    }

    private sealed record ResolvedCheckoutSubscription(
        string SubscriptionReferenceCode,
        string? CustomerReferenceCode);

    private async Task<ResolvedCheckoutSubscription?> TryResolveSubscriptionByPricingPlanAsync(
        Iyzipay.Options options,
        string pricingPlanReferenceCode,
        string? excludeSubscriptionReferenceCode,
        string? preferSubscriptionReferenceCode,
        CancellationToken cancellationToken)
    {
        var items = await SearchSubscriptionsByPricingPlanAsync(options, pricingPlanReferenceCode, "ACTIVE", cancellationToken);
        if (items == null || items.Count == 0)
            items = await SearchSubscriptionsByPricingPlanAsync(options, pricingPlanReferenceCode, null, cancellationToken);

        if (items == null || items.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(excludeSubscriptionReferenceCode))
        {
            items = items
                .Where(s => !string.Equals(s.ReferenceCode, excludeSubscriptionReferenceCode, StringComparison.Ordinal))
                .ToList();
            if (items.Count == 0)
                return null;
        }

        SubscriptionResource chosen;
        string selection;
        if (!string.IsNullOrWhiteSpace(preferSubscriptionReferenceCode))
        {
            var preferred = items.FirstOrDefault(s =>
                string.Equals(s.ReferenceCode, preferSubscriptionReferenceCode, StringComparison.Ordinal));
            if (preferred != null)
            {
                chosen = preferred;
                selection = "prefer_db_ref";
            }
            else
            {
                chosen = items.OrderByDescending(s => FromIyzicoEpoch(s.CreatedDate) ?? DateTime.MinValue).First();
                selection = "newest_created";
            }
        }
        else
        {
            chosen = items.OrderByDescending(s => FromIyzicoEpoch(s.CreatedDate) ?? DateTime.MinValue).First();
            selection = "newest_created";
        }

        AgentDebugLog.Write("H8", "IyzicoPaymentManager.TryResolveSubscriptionByPricingPlan", "pricing_plan_search", new
        {
            activeMatchCount = items.Count,
            selection,
            chosenRefLen = (chosen.ReferenceCode ?? string.Empty).Length
        }, "post-fix");

        return new ResolvedCheckoutSubscription(
            chosen.ReferenceCode ?? string.Empty,
            chosen.CustomerReferenceCode);
    }

    private static async Task<List<SubscriptionResource>?> SearchSubscriptionsByPricingPlanAsync(
        Iyzipay.Options options,
        string pricingPlanReferenceCode,
        string? subscriptionStatus,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var searchReq = new SearchSubscriptionRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = $"search_{DateTime.UtcNow:yyyyMMddHHmmss}",
            PricingPlanReferenceCode = pricingPlanReferenceCode,
            Page = 1,
            Count = 20
        };

        if (!string.IsNullOrWhiteSpace(subscriptionStatus))
            searchReq.SubscriptionStatus = subscriptionStatus;

        var searchRes = Subscription.Search(searchReq, options);
        if (searchRes == null || searchRes.Status != "success" || searchRes.Data?.Items == null)
            return null;

        return searchRes.Data.Items;
    }

    private bool IsYearlyPricingPlan(string? pricingPlanReferenceCode)
    {
        if (string.IsNullOrWhiteSpace(pricingPlanReferenceCode))
            return false;

        return string.Equals(pricingPlanReferenceCode, _settings.StarterYearlyPlanCode, StringComparison.Ordinal)
               || string.Equals(pricingPlanReferenceCode, _settings.ProYearlyPlanCode, StringComparison.Ordinal)
               || string.Equals(pricingPlanReferenceCode, _settings.BusinessYearlyPlanCode, StringComparison.Ordinal);
    }

    private static DateTime? ParseIyzicoPeriod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch))
            return FromIyzicoEpoch(epoch);

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            return parsed;

        if (DateTime.TryParse(value, new CultureInfo("tr-TR"), DateTimeStyles.AssumeLocal, out parsed))
            return parsed;

        return null;
    }

    private static DateTime? FromIyzicoEpoch(long? value)
    {
        if (!value.HasValue || value.Value <= 0)
            return null;

        try
        {
            return value.Value > 9_999_999_999
                ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value).LocalDateTime
                : DateTimeOffset.FromUnixTimeSeconds(value.Value).LocalDateTime;
        }
        catch
        {
            return null;
        }
    }

    private Iyzipay.Options CreateOptions() => new()
    {
        ApiKey = _settings.ApiKey,
        SecretKey = _settings.SecretKey,
        BaseUrl = _settings.BaseUrl
    };

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

    private string ResolvePricingPlanReferenceCode(string planType, string billingCycle)
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

