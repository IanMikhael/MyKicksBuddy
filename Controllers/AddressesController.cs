using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Repositories;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[Authorize(Roles = "customer")]
[Route("addresses")]
public class AddressesController : Controller
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
        var addresses = await _addressRepository.GetByUserIdAsync(userId);
        return Ok(addresses);
    }

   [HttpPost]
public async Task<IActionResult> Create(
    string label,
    string fullAddress,
    string latitude,  // Ubah ke string
    string longitude, // Ubah ke string
    bool isDefault = false)
{
    // Parse manual menggunakan InvariantCulture agar titik (.) selalu terbaca dengan benar
    if (!double.TryParse(latitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) ||
        !double.TryParse(longitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lng))
    {
        return BadRequest("Format latitude atau longitude tidak valid.");
    }

    var userId = GetUserId();

    var distanceKm = _distanceService.CalculateDistanceKm(lat, lng);
    var isWithinRadius = _distanceService.IsWithinRadius(distanceKm);

    if (!isWithinRadius)
        return Ok($"Ditolak: jarak {distanceKm} km melebihi 5 km");

    var address = new CustomerAddress
    {
        UserId = userId,
        Label = label,
        FullAddress = fullAddress,
        Latitude = lat,   // Gunakan variabel lat hasil parse
        Longitude = lng,  // Gunakan variabel lng hasil parse
        DistanceKm = distanceKm,
        IsWithinRadius = isWithinRadius,
        IsDefault = isDefault
    };

    var newId = await _addressRepository.CreateAsync(address);
    return Ok(new { message = "Alamat berhasil ditambahkan.", id = newId, distanceKm });
}

    [HttpPost("delete/{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var userId = GetUserId();
        await _addressRepository.DeleteAsync(id, userId);
        TempData["Success"] = "Alamat berhasil dihapus.";
        return RedirectToAction(nameof(Index));
    }

    private long GetUserId()
    {
        return long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}