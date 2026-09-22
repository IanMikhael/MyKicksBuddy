using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Services;

public interface IJwtService
{
    string GenerateToken(User user);
}