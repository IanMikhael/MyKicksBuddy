namespace MyKicksBuddy.Models.Dtos;

public class OrderResponse
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}