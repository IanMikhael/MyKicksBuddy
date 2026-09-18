using System.ComponentModel.DataAnnotations;

namespace MyKicksBuddy.Models.Dtos;

public class OrderItemRequest
{
    [Required]
    [Range(1, long.MaxValue, ErrorMessage = "ServiceId tidak valid.")]
    public long ServiceId { get; set; }

    [Required]
    [Range(1, 100, ErrorMessage = "Quantity harus antara 1 sampai 100.")]
    public int Quantity { get; set; }

    public string? ShoeDescription { get; set; }
}