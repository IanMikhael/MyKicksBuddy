namespace MyKicksBuddy.Models.Entities;

public class Order
{
    public long Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string Channel { get; set; } = "online";
    public string FulfillmentType { get; set; } = string.Empty; // pickup_delivery / drop_off
    public long? AddressId { get; set; }
    public decimal? DistanceKm { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "pending_payment";
    public string PaymentStatus { get; set; } = "unpaid";
    public long? HandledBy { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}