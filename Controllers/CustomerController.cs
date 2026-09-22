using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MyKicksBuddy.Repositories;

namespace MyKicksBuddy.Controllers;

[Authorize(Roles = "customer")]
[Route("customer")]
public class CustomerController : Controller
{
    private readonly IUserRepository _users;

    public CustomerController(IUserRepository users) => _users = users;

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var id = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var account = await _users.GetByIdAsync(id);
        return account is null || account.Role != "customer" ? NotFound() : View(account);
    }
}
