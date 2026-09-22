using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Services;
using System.Security.Claims;

namespace MyKicksBuddy.Controllers;

[ApiController]
[Route("api/staff/orders")]
[Authorize(Roles = "kasir,admin")] // Dilindungi agar hanya bisa diakses oleh kasir dan admin
public class StaffOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public StaffOrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    private long GetStaffId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim == null || !long.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Identitas petugas tidak valid.");
        return id;
    }

    /// <summary>
    /// Update status pesanan beserta log transaksional (Khusus Staff/Admin)
    /// Endpoint: PATCH /staff/orders/{id}/status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateOrderStatus(long id, [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            var staffId = GetStaffId();
            var staffRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var result = await _orderService.UpdateStatusAsync(id, request.Status, staffId, staffRole, request.Notes);
            if (!result.Success)
                return BadRequest(new { message = result.Error });

            return Ok(new { message = "Status pesanan berhasil diperbarui dan dicatat dalam log." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Melihat riwayat pelacakan (logs) status pesanan (Khusus Staff/Admin)
    /// Endpoint: GET /staff/orders/{id}/logs
    /// </summary>
    [HttpGet("{id}/logs")]
    public async Task<IActionResult> GetOrderLogs(long id)
    {
        var logs = await _orderService.GetOrderLogsAsync(id);
        return Ok(logs);
    }

    /// <summary>
    /// Melihat detail pesanan lengkap beserta item cucian berdasarkan ID (Khusus Staff/Admin)
    /// Endpoint: GET /staff/orders/{id}
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderDetail(long id)
    {
        var order = await _orderService.GetOrderDetailForStaffAsync(id);
        
        if (order == null)
        {
            return NotFound(new { message = "Pesanan tidak ditemukan." });
        }
        
        return Ok(order);
    }
}
