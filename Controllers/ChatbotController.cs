using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[ApiController]
[Route("api/chatbot")]
public class ChatbotController : ControllerBase
{
    private readonly IOrderService _orderService; // Menggunakan IOrderService (singular)

    public ChatbotController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    // 1. Cek Status Pesanan berdasarkan Order Code
    [HttpGet("orders/{orderCode}")]
    [Authorize(Roles = "customer")]
    public async Task<IActionResult> GetOrderByCode(string orderCode)
    {
        var customerId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var order = await _orderService.GetOrderByCodeAsync(orderCode, customerId);
        
        if (order == null)
        {
            return NotFound(new { message = "Maaf, pesanan dengan kode tersebut tidak ditemukan." });
        }

        return Ok(order);
    }

    // 2. Daftar Layanan
    [HttpGet("services")]
    public async Task<IActionResult> GetServices()
    {
        var services = await _orderService.GetAllServicesAsync();
        return Ok(services);
    }

}
