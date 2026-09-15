using System.Text.Json.Serialization;

namespace MyKicksBuddy.Models.Dtos;

public class MidtransNotificationRequest
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("status_code")]
    public string StatusCode { get; set; } = string.Empty;

    [JsonPropertyName("gross_amount")]
    public string GrossAmount { get; set; } = string.Empty;

    [JsonPropertyName("signature_key")]
    public string SignatureKey { get; set; } = string.Empty;

    [JsonPropertyName("transaction_status")]
    public string TransactionStatus { get; set; } = string.Empty;

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("payment_type")]
    public string? PaymentType { get; set; }

    [JsonPropertyName("fraud_status")]
    public string? FraudStatus { get; set; }
}
