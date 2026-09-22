using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Models.Dtos;

namespace MyKicksBuddy.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailOrPhoneAsync(string emailOrPhone);
    Task<bool> ExistsByEmailOrPhoneAsync(string? email, string? phone);
    Task<User?> GetByIdAsync(long id);
    Task<long> CreateAsync(User user);
    Task<IReadOnlyList<InternalUserDto>> GetInternalUsersAsync();
    Task<bool> UpdateAdminProfileAsync(long id, string fullName, string email, string? phone);
    Task<bool> UpdateAdminPasswordHashAsync(long id, string expectedHash, string newHash);
}
