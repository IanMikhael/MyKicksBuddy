using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Services;
using MyKicksBuddy.Models.Dtos;

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
    public async Task<IActionResult> GetOrderByCode(string orderCode)
    {
        var order = await _orderService.GetOrderByCodeAsync(orderCode);
        
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

    // 3. Buat Pesanan Baru (POST)
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Data pesanan tidak valid." });
        }

        // Ubah dari ID 1 menjadi ID 2 (Budi Santoso) yang sudah ada di database[cite: 2]
        long defaultCustomerId = 2; 

        var result = await _orderService.CreateOrderAsync(defaultCustomerId, request);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Error });
        }

        return Ok(new 
        { 
            message = "Pesanan berhasil dibuat!",
            orderId = result.OrderId,
            data = request
        });
    }
}