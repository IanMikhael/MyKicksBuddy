using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailOrPhoneAsync(string emailOrPhone);
    Task<User?> GetByIdAsync(long id);
    Task<long> CreateAsync(User user);
    Task UpdateSecurityStampAsync(long userId, string securityStamp);
}