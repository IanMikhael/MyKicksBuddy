using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[Route("auth")]
public class AuthController : Controller
{
    private readonly IAuthService _authService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IJwtService jwtService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _jwtService = jwtService;
        _logger = logger;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        return View();
    }

    [HttpGet("register")]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState); // Ubah dari return View(request) ke JSON

        (bool success, string? error, User? user) result;
        try
        {
            result = await _authService.RegisterAsync(request);
        }
        catch (Exception ex) when (IsDatabaseConnectionException(ex))
        {
            _logger.LogError(ex, "Database connection failed during registration.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Tidak dapat terhubung ke database. Silakan coba kembali." });
        }

        var (success, error, user) = result;
        if (!success)
        {
            return BadRequest(new { message = error }); // Kembalikan pesan error dalam format JSON
        }

        await SignInUserAsync(user!.Id, user.FullName, user.Role);
        return Ok(new { message = "Registrasi berhasil!", userId = user.Id, role = user.Role, token = _jwtService.GenerateToken(user) });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Login(LoginRequest request) => LoginCore(request, false);

    [HttpPost("admin-login")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AdminLogin(LoginRequest request) => LoginCore(request, true);

    private async Task<IActionResult> LoginCore(LoginRequest request, bool adminOnly)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        (bool success, string? error, User? user) result;
        try
        {
            result = await _authService.LoginAsync(request);
        }
        catch (Exception ex) when (IsDatabaseConnectionException(ex))
        {
            _logger.LogError(ex, "Database connection failed during login.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Tidak dapat terhubung ke database. Silakan coba kembali." });
        }

        var (success, error, user) = result;
        if (!success)
        {
            return BadRequest(new { message = error });
        }

        if (adminOnly && user!.Role != "admin")
            return BadRequest(new { message = "Akun ini tidak memiliki akses Admin." });

        await SignInUserAsync(user!.Id, user.FullName, user.Role);
        return Ok(new { message = "Login berhasil!", userId = user.Id, role = user.Role, token = _jwtService.GenerateToken(user) });
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private async Task SignInUserAsync(long userId, string fullName, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, fullName),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    private static bool IsDatabaseConnectionException(Exception ex)
    {
        return ex is MySqlException or InvalidOperationException;
    }
}
