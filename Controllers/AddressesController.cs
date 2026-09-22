using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[Authorize(Roles = "customer")]
[Route("addresses")]
public class AddressesController : Controller
{
    private readonly IAddressService _addressService;

    public AddressesController(IAddressService addressService)
    {
        _addressService = addressService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var addresses = await _addressService.GetByCustomerAsync(GetUserId());
        return View(addresses);
    }

    [HttpGet("create")]
    public IActionResult Create() => View("Form", new AddressFormModel());

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AddressFormModel model)
    {
        if (!ModelState.IsValid)
            return View("Form", model);

        var result = await _addressService.CreateAsync(GetUserId(), model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Alamat tidak dapat disimpan.");
            return View("Form", model);
        }

        TempData["Success"] = "Alamat berhasil disimpan.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id)
    {
        var address = await _addressService.GetAsync(id, GetUserId());
        if (address is null)
            return NotFound();

        return View("Form", new AddressFormModel
        {
            Id = address.Id,
            Label = address.Label,
            FullAddress = address.FullAddress,
            Latitude = address.Latitude,
            Longitude = address.Longitude,
            IsDefault = address.IsDefault
        });
    }

    [HttpPost("{id:long}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, AddressFormModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
            return View("Form", model);

        var result = await _addressService.UpdateAsync(GetUserId(), model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Alamat tidak dapat diperbarui.");
            return View("Form", model);
        }

        TempData["Success"] = "Alamat berhasil diperbarui.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:long}/default")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(long id)
    {
        if (!await _addressService.SetDefaultAsync(id, GetUserId()))
            return NotFound();

        TempData["Success"] = "Alamat utama berhasil diubah.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:long}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id)
    {
        if (!await _addressService.DeleteAsync(id, GetUserId()))
            return NotFound();

        TempData["Success"] = "Alamat berhasil dihapus.";
        return RedirectToAction(nameof(Index));
    }

    private long GetUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
