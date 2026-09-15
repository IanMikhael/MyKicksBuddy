namespace MyKicksBuddy.Models.Dtos;

public class PaymentResponse
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = "IDR";
    public string Status { get; set; } = string.Empty;
    public string? RedirectUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
}
