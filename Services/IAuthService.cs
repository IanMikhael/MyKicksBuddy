using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Services;

public interface IAuthService
{
    Task<(bool Success, string? Error, User? User)> RegisterAsync(RegisterRequest request);
    Task<(bool Success, string? Error, User? User)> LoginAsync(LoginRequest request);

    /// <summary>
    /// Rotasi security stamp user - membatalkan SEMUA token JWT yang sudah pernah
    /// diterbitkan buat user ini, di semua device, seketika.
    /// </summary>
    Task InvalidateSessionsAsync(long userId);
}