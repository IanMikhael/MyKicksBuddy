using System.ComponentModel.DataAnnotations;

namespace MyKicksBuddy.Models.Dtos;

public class CreateAddressRequest
{
    [Required(ErrorMessage = "Label alamat wajib diisi.")]
    [MaxLength(50, ErrorMessage = "Label maksimal 50 karakter.")]
    public string Label { get; set; } = string.Empty;

    [Required(ErrorMessage = "Alamat lengkap wajib diisi.")]
    public string FullAddress { get; set; } = string.Empty;

    [Range(-90, 90, ErrorMessage = "Latitude tidak valid.")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude tidak valid.")]
    public double Longitude { get; set; }

    public bool IsDefault { get; set; }
}
