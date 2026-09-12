using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Services;

public interface IAuthService
{
    Task<(bool Success, string? Error, User? User)> RegisterAsync(RegisterRequest request);
    Task<(bool Success, string? Error, User? User)> LoginAsync(LoginRequest request);
}