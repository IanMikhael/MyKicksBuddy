namespace MyKicksBuddy.Models.Dtos;

public class StaffOrderListResponse
{
    public long Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string FulfillmentType { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public long? HandledBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
