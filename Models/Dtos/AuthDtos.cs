using System.ComponentModel.DataAnnotations;

namespace MyKicksBuddy.Models.Dtos;

public class RegisterRequest
{
    [Required(ErrorMessage = "Nama lengkap wajib diisi.")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Format email tidak valid.")]
    public string? Email { get; set; }

    public string? Phone { get; set; }

    [Required(ErrorMessage = "Password wajib diisi.")]
    [MinLength(6, ErrorMessage = "Password minimal 6 karakter.")]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required(ErrorMessage = "Email atau nomor telepon wajib diisi.")]
    public string EmailOrPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password wajib diisi.")]
    public string Password { get; set; } = string.Empty;
}