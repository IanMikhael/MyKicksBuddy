using System.ComponentModel.DataAnnotations;

namespace MyKicksBuddy.Models.Dtos;

public class UpdateOrderStatusRequest
{
    [Required(ErrorMessage = "Status wajib diisi.")]
    public string Status { get; set; } = string.Empty;

    [MaxLength(255, ErrorMessage = "Catatan maksimal 255 karakter.")]
    public string? Notes { get; set; }
}