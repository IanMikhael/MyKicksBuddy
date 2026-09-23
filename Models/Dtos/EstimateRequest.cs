using System.ComponentModel.DataAnnotations;
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
    [Range(1, int.MaxValue, ErrorMessage = "ServiceId tidak valid.")]
    public int ServiceId { get; set; }

    [JsonPropertyName("quantity")]
    [Range(1, 100, ErrorMessage = "Quantity harus antara 1 sampai 100.")]
    public int Quantity { get; set; }
}