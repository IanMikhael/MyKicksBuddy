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
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.Phone))
            return (false, "Email atau nomor telepon wajib diisi.", null);

        var identifier = string.IsNullOrWhiteSpace(request.Email) ? request.Phone! : request.Email;
        var existing = await _userRepository.GetByEmailOrPhoneAsync(identifier);
        if (existing is not null)
            return (false, "Email atau nomor telepon sudah terdaftar.", null);

        var user = new User
        {
            Role = "customer",
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            IsActive = true
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        var newId = await _userRepository.CreateAsync(user);
        user.Id = newId;

        return (true, null, user);
    }

    public async Task<(bool Success, string? Error, User? User)> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailOrPhoneAsync(request.EmailOrPhone);
        if (user is null || !user.IsActive)
            return (false, "Email/nomor telepon atau password salah.", null);

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return (false, "Email/nomor telepon atau password salah.", null);

        return (true, null, user);
    }
}