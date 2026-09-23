using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[Route("auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IJwtService _jwtService;

    public AuthController(IAuthService authService, IJwtService jwtService)
    {
        _authService = authService;
        _jwtService = jwtService;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (success, error, user) = await _authService.RegisterAsync(request);
        if (!success)
            return BadRequest(new { message = error });

        var token = _jwtService.GenerateToken(user!);

        return Ok(new
        {
            message = "Registrasi berhasil!",
            userId = user!.Id,
            role = user.Role,
            token
        });
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (success, error, user) = await _authService.LoginAsync(request);
        if (!success)
            return BadRequest(new { message = error });

        var token = _jwtService.GenerateToken(user!);

        return Ok(new
        {
            message = "Login berhasil!",
            userId = user!.Id,
            role = user.Role,
            token
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // JWT stateless, logout cukup hapus token di sisi client
        return Ok(new { message = "Logout berhasil. Hapus token di sisi client." });
    }
}