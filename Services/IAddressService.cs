using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Services;

public interface IAddressService
{
    Task<IReadOnlyList<CustomerAddress>> GetByCustomerAsync(long customerId);
    Task<CustomerAddress?> GetAsync(long id, long customerId);
    Task<(bool Success, string? Error, long Id)> CreateAsync(long customerId, AddressFormModel model);
    Task<(bool Success, string? Error)> UpdateAsync(long customerId, AddressFormModel model);
    Task<bool> DeleteAsync(long id, long customerId);
    Task<bool> SetDefaultAsync(long id, long customerId);
}
