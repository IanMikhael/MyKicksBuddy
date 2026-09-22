using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Repositories;

namespace MyKicksBuddy.Services;

public class AddressService : IAddressService
{
    private readonly IAddressRepository _repository;
    private readonly DistanceService _distanceService;

    public AddressService(IAddressRepository repository, DistanceService distanceService)
    {
        _repository = repository;
        _distanceService = distanceService;
    }

    public async Task<IReadOnlyList<CustomerAddress>> GetByCustomerAsync(long customerId) =>
        (await _repository.GetByUserIdAsync(customerId)).ToList();

    public Task<CustomerAddress?> GetAsync(long id, long customerId) =>
        _repository.GetByIdAsync(id, customerId);

    public async Task<(bool Success, string? Error, long Id)> CreateAsync(long customerId, AddressFormModel model)
    {
        var address = BuildAddress(customerId, model);
        var id = await _repository.CreateAsync(address);
        return (true, null, id);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(long customerId, AddressFormModel model)
    {
        if (!model.Id.HasValue)
            return (false, "Alamat tidak valid.");

        var address = BuildAddress(customerId, model);
        address.Id = model.Id.Value;
        var updated = await _repository.UpdateAsync(address);
        return updated ? (true, null) : (false, "Alamat tidak ditemukan atau bukan milik Anda.");
    }

    public Task<bool> DeleteAsync(long id, long customerId) => _repository.DeleteAsync(id, customerId);
    public Task<bool> SetDefaultAsync(long id, long customerId) => _repository.SetDefaultAsync(id, customerId);

    private CustomerAddress BuildAddress(long customerId, AddressFormModel model)
    {
        var latitude = model.Latitude!.Value;
        var longitude = model.Longitude!.Value;
        var distance = _distanceService.CalculateDistanceKm(latitude, longitude);

        return new CustomerAddress
        {
            UserId = customerId,
            Label = model.Label.Trim(),
            FullAddress = model.FullAddress.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            DistanceKm = distance,
            IsWithinRadius = _distanceService.IsWithinRadius(distance),
            IsDefault = model.IsDefault
        };
    }
}
