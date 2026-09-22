using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Repositories;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[Authorize(Roles = "customer")]
[Route("addresses")]
[ApiController]
public class AddressesController : ControllerBase
{
    private readonly IAddressRepository _addressRepository;
    private readonly DistanceService _distanceService;

    public AddressesController(IAddressRepository addressRepository, DistanceService distanceService)
    {
        _addressRepository = addressRepository;
        _distanceService = distanceService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var addresses = await _addressRepository.GetByUserIdAsync(userId.Value);
        return Ok(addresses);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAddressRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var distanceKm = _distanceService.CalculateDistanceKm(request.Latitude, request.Longitude);
        var isWithinRadius = _distanceService.IsWithinRadius(distanceKm);

        if (!isWithinRadius)
            return BadRequest(new { message = $"Alamat berjarak {distanceKm} km dari toko, melebihi batas layanan antar-jemput 5 km." });

        var address = new CustomerAddress
        {
            UserId = userId.Value,
            Label = request.Label,
            FullAddress = request.FullAddress,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            DistanceKm = distanceKm,
            IsWithinRadius = isWithinRadius,
            IsDefault = request.IsDefault
        };

        var newId = await _addressRepository.CreateAsync(address);
        return Ok(new { message = "Alamat berhasil ditambahkan.", id = newId, distanceKm });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            await _addressRepository.DeleteAsync(id, userId.Value);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }

        return Ok(new { message = "Alamat berhasil dihapus." });
    }

    private long? GetUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(claimValue, out var id) ? id : null;
    }
}