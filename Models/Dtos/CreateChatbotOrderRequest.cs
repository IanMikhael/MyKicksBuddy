using System.ComponentModel.DataAnnotations;

namespace MyKicksBuddy.Models.Dtos;

public class CreateChatbotOrderRequest
{
    [Required(ErrorMessage = "Nama pelanggan wajib diisi.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nomor HP pelanggan wajib diisi.")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tipe fulfillment wajib diisi.")]
    [RegularExpression("^(pickup_delivery|drop_off)$",
        ErrorMessage = "FulfillmentType harus 'pickup_delivery' atau 'drop_off'.")]
    public string FulfillmentType { get; set; } = string.Empty;

    public long? AddressId { get; set; }

    [MaxLength(255, ErrorMessage = "Catatan maksimal 255 karakter.")]
    public string? Notes { get; set; }

    [Required(ErrorMessage = "Item pesanan tidak boleh kosong.")]
    [MinLength(1, ErrorMessage = "Minimal harus ada 1 item pesanan.")]
    public List<OrderItemRequest> Items { get; set; } = new();
}
