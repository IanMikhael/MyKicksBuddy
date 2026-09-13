using System.Text.Json.Serialization;

namespace MyKicksBuddy.Models.Dtos;

public class EstimateRequest
{
    [JsonPropertyName("items")]
    public List<EstimateItemRequest> Items { get; set; } = new();
}

public class EstimateItemRequest
{
    [JsonPropertyName("serviceId")]
    public int ServiceId { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}