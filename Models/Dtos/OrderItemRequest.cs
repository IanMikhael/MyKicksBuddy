namespace MyKicksBuddy.Models.Dtos;

public class OrderItemRequest
{
    public long ServiceId { get; set; }
    public int Quantity { get; set; }
    public string? ShoeDescription { get; set; }
}