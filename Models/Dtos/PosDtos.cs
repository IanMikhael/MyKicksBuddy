using System.ComponentModel.DataAnnotations;

namespace MyKicksBuddy.Models.Dtos;

public class CreatePosOrderRequest
{
    [Required(ErrorMessage = "Nama pelanggan wajib diisi.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nomor HP pelanggan wajib diisi.")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Metode pembayaran wajib diisi.")]
    [RegularExpression("^(cash|midtrans)$", ErrorMessage = "PaymentMethod harus 'cash' atau 'midtrans'.")]
    public string PaymentMethod { get; set; } = "cash";

    public string? Notes { get; set; }

    [Required(ErrorMessage = "Item pesanan tidak boleh kosong.")]
    [MinLength(1, ErrorMessage = "Minimal harus ada 1 item pesanan.")]
    public List<OrderItemRequest> Items { get; set; } = new();
}
