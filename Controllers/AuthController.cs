using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    public IActionResult Login() => View();

    [HttpGet("register")]
    public IActionResult Register() => View();

    // NB integrasi: request JSON (Content-Type: application/json), bukan form-urlencoded -
    // ini kontrak yang sudah didokumentasikan & dites dari awal (lihat riwayat FRONTEND_GUIDE).
    // wwwroot/js/auth.js versi lama masih kirim form-urlencoded dan perlu disesuaikan.
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

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
            return BadRequest(new { message = error });

        var token = _jwtService.GenerateToken(user!);
        SetAuthCookie(token);

        return Ok(new { message = "Registrasi berhasil!", userId = user!.Id, role = user.Role, token });
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public Task<IActionResult> Login([FromBody] LoginRequest request) => LoginCore(request, adminOnly: false);

    [HttpPost("admin-login")]
    [EnableRateLimiting("auth")]
    public Task<IActionResult> AdminLogin([FromBody] LoginRequest request) => LoginCore(request, adminOnly: true);

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
            return BadRequest(new { message = error });

        if (adminOnly && user!.Role != "admin")
            return BadRequest(new { message = "Akun ini tidak memiliki akses Admin." });

        var token = _jwtService.GenerateToken(user!);
        SetAuthCookie(token);

        return Ok(new { message = "Login berhasil!", userId = user!.Id, role = user.Role, token });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (long.TryParse(userIdClaim, out var userId))
        {
            // Rotasi security stamp: token ini (dan token lain yang pernah diterbitkan
            // untuk user ini, di device manapun) langsung berhenti valid, bukan cuma
            // dihapus dari cookie/client.
            await _authService.InvalidateSessionsAsync(userId);
        }

        Response.Cookies.Delete(JwtService.CookieName);

        if (WantsHtml())
            return RedirectToAction("Index", "Home");

        return Ok(new { message = "Logout berhasil. Semua sesi untuk akun ini sudah dicabut." });
    }

    // JWT yang sama dipakai buat browser (lewat cookie) maupun API/chatbot/POS (lewat
    // header Authorization) - satu-satunya "kendaraan" yang beda, validasinya tetap satu
    // jalur di Program.cs. HttpOnly cegah dibaca JavaScript (mitigasi XSS), SameSite=Strict
    // cegah browser otomatis mengirim cookie ini ke request lintas-situs (mitigasi CSRF)
    // tanpa perlu anti-forgery token terpisah di tiap form.
    private void SetAuthCookie(string token)
    {
        Response.Cookies.Append(JwtService.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(2) // samakan dengan masa berlaku token di JwtService
        });
    }

    private bool WantsHtml() =>
        Request.Headers.Accept.Any(a => a != null && a.Contains("text/html", StringComparison.OrdinalIgnoreCase));

    private static bool IsDatabaseConnectionException(Exception ex) => ex is MySqlException or InvalidOperationException;
}
