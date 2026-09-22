using MyKicksBuddy.Services;

namespace MyKicksBuddy.Models.Dtos;

public class OrderDetailResponse
{
    public long Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string FulfillmentType { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? AddressLabel { get; set; }
    public string? FullAddress { get; set; }
    public decimal? DistanceKm { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public string StatusLabel => OrderStatusWorkflow.DisplayLabel(Status, PaymentStatus);
    public string HistoryGroup => OrderStatusWorkflow.HistoryGroup(Status);
    public int ProgressStage => OrderStatusWorkflow.ProgressStage(Status);
    public string ProgressDescription => OrderStatusWorkflow.ProgressDescription(Status, PaymentStatus);
}
