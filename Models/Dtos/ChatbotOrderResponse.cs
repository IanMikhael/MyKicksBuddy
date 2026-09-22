namespace MyKicksBuddy.Models.Dtos;

public class ChatbotOrderResponse
{
    public string OrderCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string FulfillmentType { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ChatbotOrderItemResponse> Items { get; set; } = new();
}

public class ChatbotOrderItemResponse
{
    public string ServiceName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
}

public class ChatbotCreateOrderResponse
{
    public string OrderCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}