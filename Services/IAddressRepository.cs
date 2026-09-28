using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Repositories;

public interface IAddressRepository
{
    Task<IEnumerable<CustomerAddress>> GetByUserIdAsync(long userId);
    Task<CustomerAddress?> GetByIdAsync(long id, long userId);
    Task<long> CreateAsync(CustomerAddress address);
    Task<bool> UpdateAsync(CustomerAddress address);
    Task<bool> DeleteAsync(long id, long userId);
    Task<bool> SetDefaultAsync(long id, long userId);
}
