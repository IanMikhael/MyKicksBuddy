namespace MyKicksBuddy.Models.Dtos;

public class OrderStatusLogResponse
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public long? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}