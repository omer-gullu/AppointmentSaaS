using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Diagnostics;
using Appointment_SaaS.Core.Entities;
using Appointment_SaaS.Core.Utilities;
using Appointment_SaaS.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Appointment_SaaS.API.Controller;

[ApiController]
[Route("api/iyzico/webhook")]
public class IyzicoWebhookController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ITenantPlanService _tenantPlanService;
    private readonly IyzicoSettings _settings;
    private readonly ILogger<IyzicoWebhookController> _logger;
    private readonly AppDbContext _db;

    public IyzicoWebhookController(
        ITenantService tenantService,
        ITenantPlanService tenantPlanService,
        IOptions<IyzicoSettings> settings,
        ILogger<IyzicoWebhookController> logger,
        AppDbContext db)
    {
        _tenantService = tenantService;
        _tenantPlanService = tenantPlanService;
        _settings = settings.Value;
        _logger = logger;
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("[IyzicoWebhook] Boş body alındı. IP={IP}",
                HttpContext.Connection.RemoteIpAddress);
            return BadRequest(new { message = "Empty body." });
        }

        if (string.IsNullOrWhiteSpace(_settings.WebhookSecret))
        {
            _logger.LogError("[IyzicoWebhook] WebhookSecret is not configured.");
            return StatusCode(503, new { message = "Service Unavailable" });
        }

        var signatureHeader = Request.Headers["X-Iyzico-Signature"].FirstOrDefault()
                              ?? Request.Headers["Iyzico-Signature"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            _logger.LogWarning("[IyzicoWebhook] Signature header eksik. IP={IP}",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Missing signature header." });
        }

        if (!VerifyHmacSignature(body, signatureHeader, _settings.WebhookSecret))
        {
            _logger.LogWarning("[IyzicoWebhook] Geçersiz HMAC imzası. IP={IP}",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Invalid webhook signature." });
        }

        JsonElement json;
        try
        {
            json = JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[IyzicoWebhook] JSON parse hatası. Body={Body}", body);
            return BadRequest(new { message = "Invalid JSON." });
        }

        var eventType = TryGetString(json, "eventType")
                        ?? TryGetString(json, "event")
                        ?? TryGetString(json, "type");

        var referenceCode =
            TryGetString(json, "subscriptionReferenceCode")
            ?? TryGetString(json, "referenceCode")
            ?? TryGetString(json, "subscriptionReference")
            ?? TryGetNestedString(json, "data", "referenceCode")
            ?? TryGetNestedString(json, "data", "subscriptionReferenceCode");

        var paymentId =
            TryGetString(json, "paymentId")
            ?? TryGetString(json, "paymentTransactionId")
            ?? TryGetNestedString(json, "data", "paymentId");

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrWhiteSpace(referenceCode))
        {
            _logger.LogWarning("[IyzicoWebhook] Referans kodu eksik. Body={Body}", body);
            return BadRequest(new { message = "Missing subscription reference code." });
        }

        try
        {
            var tenant = await _tenantService.GetBySubscriptionReferenceAsync(referenceCode);
            if (tenant == null)
            {
                _logger.LogWarning("[IyzicoWebhook] Tenant bulunamadı. Ref={Ref}", referenceCode);
                return NotFound(new { message = "Tenant not found." });
            }

            var normalized = (eventType ?? "").Trim().ToLowerInvariant();

            if (IsPaymentFailure(normalized))
            {
                AgentDebugLog.Write("H-A", "IyzicoWebhook.Receive", "payment_failed_event", new
                {
                    tenant.TenantID,
                    tenant.IsActive,
                    tenant.PlanType,
                    hasPending = !string.IsNullOrWhiteSpace(tenant.PendingPlanType),
                    eventType = normalized
                });
                await HandlePaymentFailureAsync(tenant, body, paymentId, clientIp);
            }
            else if (IsPaymentSuccess(normalized) || IsUpgrade(normalized))
            {
                await HandlePaymentSuccessAsync(tenant, json, body, paymentId, clientIp, isUpgrade: IsUpgrade(normalized));
            }
            else if (IsRefundOrChargeback(normalized))
            {
                await HandleRefundAsync(tenant, body, paymentId, clientIp);
            }
            else if (IsCancellation(normalized))
            {
                _logger.LogInformation("[IyzicoWebhook] Abonelik iptal edildi. Ref={Ref}, TenantId={TenantId}",
                    referenceCode, tenant.TenantID);
                await _tenantService.UpdateSubscriptionStatusAsync(tenant, isActive: false);
            }
            else
            {
                _logger.LogInformation("[IyzicoWebhook] Bilinmeyen eventType={EventType}. Ref={Ref}",
                    eventType, referenceCode);
            }

            return Ok(new { status = "ok" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IyzicoWebhook] İşlem sırasında hata. Ref={Ref}", referenceCode);
            return StatusCode(500, new { message = "Internal error processing webhook." });
        }
    }

    private async Task HandlePaymentSuccessAsync(
        Tenant tenant,
        JsonElement json,
        string rawBody,
        string? paymentId,
        string? clientIp,
        bool isUpgrade)
    {
        if (!string.IsNullOrWhiteSpace(paymentId))
        {
            var alreadyProcessed = await _db.TransactionLogs
                .AnyAsync(t => t.PaymentId == paymentId && t.TransactionType == "PaymentSuccess");

            if (alreadyProcessed)
            {
                _logger.LogInformation("[IyzicoWebhook] PaymentId zaten işlendi (idempotent). PaymentId={PaymentId}", paymentId);
                return;
            }
        }

        var webhookSubRef =
            TryGetString(json, "subscriptionReferenceCode")
            ?? TryGetNestedString(json, "data", "subscriptionReferenceCode");
        if (!string.IsNullOrWhiteSpace(webhookSubRef))
            tenant.SubscriptionReferenceCode = webhookSubRef;

        await _tenantPlanService.ApplySubscriptionFromWebhookAsync(
            tenant, paymentId, rawBody, isSuccess: true, isFailure: false, isUpgrade);

        await _tenantService.UpdateSubscriptionStatusAsync(tenant, true);

        var agreementVersion = TryGetString(json, "agreementVersion")
                               ?? TryGetNestedString(json, "data", "agreementVersion")
                               ?? "v1.0";

        _db.TransactionLogs.Add(new TransactionLog
        {
            TenantId = tenant.TenantID,
            PaymentId = paymentId,
            SubscriptionReferenceCode = tenant.SubscriptionReferenceCode,
            TransactionType = isUpgrade ? "SubscriptionUpgraded" : "PaymentSuccess",
            Status = "Success",
            IpAddress = clientIp,
            AgreementVersion = agreementVersion,
            RawPayload = rawBody,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("[IyzicoWebhook] Ödeme başarılı. TenantId={TenantId}, PaymentId={PaymentId}, Upgrade={Upgrade}",
            tenant.TenantID, paymentId, isUpgrade);
    }

    private async Task HandlePaymentFailureAsync(
        Tenant tenant,
        string rawBody,
        string? paymentId,
        string? clientIp)
    {
        _logger.LogWarning("[IyzicoWebhook] Ödeme başarısız. TenantId={TenantId}, PaymentId={PaymentId}",
            tenant.TenantID, paymentId);

        await _tenantPlanService.ApplySubscriptionFromWebhookAsync(
            tenant, paymentId, rawBody, isSuccess: false, isFailure: true, isUpgrade: false);

        if (!string.IsNullOrWhiteSpace(paymentId))
        {
            _db.TransactionLogs.Add(new TransactionLog
            {
                TenantId = tenant.TenantID,
                PaymentId = paymentId,
                SubscriptionReferenceCode = tenant.SubscriptionReferenceCode,
                TransactionType = "PaymentFailed",
                Status = "Failed",
                IpAddress = clientIp,
                RawPayload = rawBody,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }

    private async Task HandleRefundAsync(
        Tenant tenant,
        string rawBody,
        string? paymentId,
        string? clientIp)
    {
        _logger.LogWarning("[IyzicoWebhook] İade bildirimi. TenantId={TenantId}, PaymentId={PaymentId}",
            tenant.TenantID, paymentId);

        await _tenantService.SuspendForRefundAsync(tenant, clientIp, rawBody, paymentId);
    }

    public static bool VerifyHmacSignature(string body, string signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(secret))
            return false;

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        using var hmac = new HMACSHA256(keyBytes);
        var computedHash = hmac.ComputeHash(bodyBytes);

        var computedBase64 = Convert.ToBase64String(computedHash);
        var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        return string.Equals(signature, computedBase64, StringComparison.Ordinal)
               || string.Equals(signature, computedHex, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPaymentSuccess(string e) =>
        e == "payment.success" || e == "subscription.order.success";

    private static bool IsPaymentFailure(string e) =>
        e == "payment.failed" || e == "subscription.order.failure";

    private static bool IsUpgrade(string e) =>
        e.Contains("upgraded") || e == "subscription.upgraded";

    private static bool IsRefundOrChargeback(string e) =>
        e.Contains("refund") || e.Contains("chargeback") || e.Contains("dispute");

    private static bool IsCancellation(string e) =>
        e.Contains("cancel") || e.Contains("cancelled") || e.Contains("canceled")
        || e.Contains("subscription.cancel");

    private static string? TryGetString(JsonElement json, string prop)
    {
        if (json.ValueKind != JsonValueKind.Object) return null;
        if (json.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return null;
    }

    private static string? TryGetNestedString(JsonElement json, string parent, string prop)
    {
        if (json.ValueKind != JsonValueKind.Object) return null;
        if (!json.TryGetProperty(parent, out var p) || p.ValueKind != JsonValueKind.Object) return null;
        if (p.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return null;
    }
}
