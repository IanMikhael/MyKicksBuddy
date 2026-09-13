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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var customerId = GetCustomerId();
        if (customerId is null) return Unauthorized();

        var result = await _orderService.CreateOrderAsync(customerId.Value, request);

        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Pesanan berhasil dibuat!", orderId = result.OrderId });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyOrders()
    {
        var customerId = GetCustomerId();
        if (customerId is null) return Unauthorized();

        var orders = await _orderService.GetOrdersByCustomerAsync(customerId.Value);
        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderDetail(long id)
    {
        var customerId = GetCustomerId();
        if (customerId is null) return Unauthorized();

        var order = await _orderService.GetOrderDetailAsync(id, customerId.Value);

        if (order == null)
            return NotFound(new { message = "Pesanan tidak ditemukan atau bukan milik Anda." });

        return Ok(order);
    }

    private long? GetCustomerId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
        return long.TryParse(claimValue, out var id) ? id : null;
    }
}