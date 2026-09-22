using System.ComponentModel.DataAnnotations;

namespace MyKicksBuddy.Models.Dtos;

public class AddressFormModel
{
    public long? Id { get; set; }

    [Required(ErrorMessage = "Label alamat wajib diisi.")]
    [StringLength(100)]
    public string Label { get; set; } = string.Empty;

    [Required(ErrorMessage = "Alamat lengkap wajib diisi.")]
    [StringLength(1000)]
    public string FullAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Latitude wajib diisi.")]
    [Range(-90, 90, ErrorMessage = "Latitude harus antara -90 dan 90.")]
    public double? Latitude { get; set; }

    [Required(ErrorMessage = "Longitude wajib diisi.")]
    [Range(-180, 180, ErrorMessage = "Longitude harus antara -180 dan 180.")]
    public double? Longitude { get; set; }

    public bool IsDefault { get; set; }
}
