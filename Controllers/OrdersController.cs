using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[Authorize(Roles = "customer")]
[Route("orders")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Membuat pesanan baru (Khusus Customer)
    /// Endpoint: POST /orders
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var customerId = GetCustomerId();
        var (success, error, orderId) = await _orderService.CreateOrderAsync(customerId, request);

        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Pesanan berhasil dibuat!", orderId });
    }

    /// <summary>
    /// Melihat daftar seluruh pesanan milik customer yang sedang login
    /// Endpoint: GET /orders
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyOrders()
    {
        var customerId = GetCustomerId();
        var orders = await _orderService.GetOrdersByCustomerAsync(customerId);
        
        return Ok(orders);
    }

    /// <summary>
    /// Melihat detail pesanan lengkap beserta item cucian berdasarkan ID (Khusus Customer)
    /// Endpoint: GET /orders/{id}
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderDetail(long id)
    {
        var customerId = GetCustomerId();
        var order = await _orderService.GetOrderDetailAsync(id, customerId);
        
        if (order == null)
        {
            return NotFound(new { message = "Pesanan tidak ditemukan atau bukan milik Anda." });
        }
        
        return Ok(order);
    }

    private long GetCustomerId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
        return long.TryParse(claimValue, out var id) ? id : 0;
    }
}