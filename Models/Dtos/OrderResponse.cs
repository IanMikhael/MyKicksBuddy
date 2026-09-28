using MyKicksBuddy.Services;

namespace MyKicksBuddy.Models.Dtos;

public class OrderResponse
{
    public long Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string? ServiceName { get; set; }
    public long CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string FulfillmentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string StatusLabel => OrderStatusWorkflow.DisplayLabel(Status, PaymentStatus);
    public string HistoryGroup => OrderStatusWorkflow.HistoryGroup(Status);
    public int ProgressStage => OrderStatusWorkflow.ProgressStage(Status);
    public string ProgressDescription => OrderStatusWorkflow.ProgressDescription(Status, PaymentStatus);
    public bool IsActive => HistoryGroup == "active";
}
