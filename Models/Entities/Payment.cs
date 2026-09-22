namespace MyKicksBuddy.Models.Entities;

public class Payment
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string ProviderOrderId { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = "IDR";
    public string Status { get; set; } = "creating";
    public string? SnapToken { get; set; }
    public string? RedirectUrl { get; set; }
    public string? PaymentType { get; set; }
    public string? FraudStatus { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
