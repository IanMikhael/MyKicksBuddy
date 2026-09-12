namespace MyKicksBuddy.Models.Dtos;

public class CreateOrderRequest
{
    public long CustomerId { get; set; }
    public string FulfillmentType { get; set; } = string.Empty;
    public long? AddressId { get; set; }
    public string? Notes { get; set; }
    public List<OrderItemRequest> Items { get; set; } = new();
}