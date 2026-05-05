using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Appointment_SaaS.Core.Entities;

/// <summary>
/// Finansal olayların (ödeme başarılı, iade, iptal) kanıt kaydı.
/// İtiraz (Chargeback) riskine karşı; ödeme anındaki IP, onay saati
/// ve Iyzico paymentId'si Unique Index ile mühürlenir (Idempotency).
/// </summary>
public class TransactionLog
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TransactionLogID { get; set; }

    public int TenantId { get; set; }

    /// <summary>Iyzico'dan gelen benzersiz ödeme/sipariş ID'si (Idempotency Key).</summary>
    [MaxLength(256)]
    public string? PaymentId { get; set; }

    /// <summary>İlgili abonelik referans kodu.</summary>
    [MaxLength(256)]
    public string? SubscriptionReferenceCode { get; set; }

    /// <summary>İşlem tipi: PaymentSuccess, Refund, Cancel, Chargeback</summary>
    [MaxLength(64)]
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>İşlem sonucu: Success, Failed, Pending</summary>
    [MaxLength(32)]
    public string Status { get; set; } = string.Empty;

    public decimal? Amount { get; set; }

    [MaxLength(8)]
    public string? Currency { get; set; }

    /// <summary>Ödeme anındaki müşteri IP adresi (Chargeback kanıtı).</summary>
    [MaxLength(64)]
    public string? IpAddress { get; set; }

    /// <summary>Kabul edilen sözleşme sürümü.</summary>
    [MaxLength(32)]
    public string? AgreementVersion { get; set; }

    /// <summary>Iyzico'dan gelen ham JSON payload (tam kanıt).</summary>
    public string? RawPayload { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
