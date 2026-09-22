using System.ComponentModel.DataAnnotations;

namespace MyKicksBuddy.Models.Dtos;

public class CreateOrderRequest
{
    [Required(ErrorMessage = "Tipe fulfillment wajib diisi.")]
    [RegularExpression("^(pickup_delivery|drop_off)$", ErrorMessage = "FulfillmentType harus 'pickup_delivery' atau 'drop_off'.")]
    public string FulfillmentType { get; set; } = string.Empty;
    public long? AddressId { get; set; }
    [MaxLength(255, ErrorMessage = "Catatan maksimal 255 karakter.")]
    public string? Notes { get; set; }
    [Required(ErrorMessage = "Item pesanan tidak boleh kosong.")]
    [MinLength(1, ErrorMessage = "Pilih minimal satu layanan.")]
    public List<OrderItemRequest> Items { get; set; } = new();
}
