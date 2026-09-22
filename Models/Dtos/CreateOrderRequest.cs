using System.ComponentModel.DataAnnotations;

namespace MyKicksBuddy.Models.Dtos;

public class CreateOrderRequest
{
    [Required]
    public string FulfillmentType { get; set; } = string.Empty;

    public long? AddressId { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [MinLength(1, ErrorMessage = "Pilih minimal satu layanan.")]
    public List<OrderItemRequest> Items { get; set; } = new();
}
