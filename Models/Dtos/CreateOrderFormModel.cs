using System.ComponentModel.DataAnnotations;
using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Models.Dtos;

public class CreateOrderFormModel
{
    [Range(1, long.MaxValue, ErrorMessage = "Pilih layanan yang tersedia.")]
    public long ServiceId { get; set; }

    [Range(1, 20, ErrorMessage = "Jumlah sepatu harus antara 1 dan 20.")]
    public int Quantity { get; set; } = 1;

    [Required(ErrorMessage = "Jelaskan sepatu yang akan dirawat.")]
    [StringLength(500)]
    public string ShoeDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Pilih metode penyerahan sepatu.")]
    public string FulfillmentType { get; set; } = "drop_off";

    public long? AddressId { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public IReadOnlyList<ServiceOptionDto> Services { get; set; } = Array.Empty<ServiceOptionDto>();
    public IReadOnlyList<CustomerAddress> Addresses { get; set; } = Array.Empty<CustomerAddress>();
}
