namespace MyKicksBuddy.Models.Dtos;

public class EstimateResponse
{
    public decimal TotalPrice { get; set; }
    public int EstimatedHours { get; set; }
    public string EstimatedCompletionText { get; set; } = string.Empty;
    public List<EstimateDetailItemResponse> Details { get; set; } = new();
}

public class EstimateDetailItemResponse
{
    public string ServiceName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal SubtotalPrice { get; set; }
    public int ServiceEstimatedHours { get; set; }
}