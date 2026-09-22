using Microsoft.AspNetCore.Identity;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Repositories;

namespace MyKicksBuddy.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<(bool Success, string? Error, User? User)> RegisterAsync(RegisterRequest request)
    {
        var fullName = request.FullName.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        if (email is null && phone is null)
            return (false, "Email atau nomor telepon wajib diisi.", null);

        if (fullName.Length == 0)
            return (false, "Nama lengkap wajib diisi.", null);

        if (await _userRepository.ExistsByEmailOrPhoneAsync(email, phone))
            return (false, "Email atau nomor telepon sudah terdaftar.", null);

        var user = new User
        {
            Role = "customer",
            FullName = fullName,
            Email = email,
            Phone = phone,
            IsActive = true
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        var newId = await _userRepository.CreateAsync(user);
        user.Id = newId;

        return (true, null, user);
    }

    public async Task<(bool Success, string? Error, User? User)> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailOrPhoneAsync(request.EmailOrPhone.Trim());
        if (user is null || !user.IsActive)
            return (false, "Email/nomor telepon atau password salah.", null);

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return (false, "Email/nomor telepon atau password salah.", null);

        return (true, null, user);
    }
}
