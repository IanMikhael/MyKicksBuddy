namespace MyKicksBuddy.Models.Entities;

// One provider payment attempt per order. No row is created until a real
// provider is configured and successfully creates the transaction.
public sealed class Payment
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "IDR";
    public string Status { get; set; } = Services.PaymentStatusWorkflow.Unpaid;
    public DateTime? VerifiedAt { get; set; }
}
