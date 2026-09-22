using System.ComponentModel.DataAnnotations;

namespace MyKicksBuddy.Models.Dtos;

public class OrderItemRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Layanan wajib dipilih.")]
    public long ServiceId { get; set; }

    [Range(1, 20, ErrorMessage = "Jumlah sepatu harus antara 1 dan 20.")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "Detail sepatu wajib diisi.")]
    [StringLength(500)]
    public string? ShoeDescription { get; set; }
}
