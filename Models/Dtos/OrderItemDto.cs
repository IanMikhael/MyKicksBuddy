namespace MyKicksBuddy.Models.Dtos;

public class OrderItemDto
{
    public long ItemId { get; set; }
    public long ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Subtotal { get; set; }
    public string? ShoeDescription { get; set; }
}
