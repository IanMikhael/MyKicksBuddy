using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Repositories;

public interface IAddressRepository
{
    Task<IEnumerable<CustomerAddress>> GetByUserIdAsync(long userId);
    Task<CustomerAddress?> GetByIdAsync(long id);
    Task<long> CreateAsync(CustomerAddress address);
    Task DeleteAsync(long id, long userId);
}