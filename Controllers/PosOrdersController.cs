using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[ApiController]
[Route("pos/orders")]
[Authorize(Roles = "kasir,admin")]
public class PosOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;

    public PosOrdersController(IOrderService orderService, IPaymentService paymentService)
    {
        _orderService = orderService;
        _paymentService = paymentService;
    }

    /// <summary>
    /// Buat transaksi POS on-the-spot. Pelanggan dicari/dibuat otomatis berdasarkan nomor HP.
    /// Endpoint: POST /pos/orders
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePosOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var staffId = GetStaffId();
        if (staffId is null) return Unauthorized();

        var result = await _orderService.CreatePosOrderAsync(staffId.Value, request);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new
        {
            message = "Transaksi POS berhasil dibuat.",
            orderId = result.OrderId,
            customerId = result.CustomerId
        });
    }

    /// <summary>
    /// Buat sesi pembayaran Midtrans (QRIS/EDC) untuk transaksi POS yang tidak dibayar cash.
    /// Endpoint: POST /pos/orders/{orderId}/payments
    /// </summary>
    [HttpPost("{orderId}/payments")]
    public async Task<IActionResult> CreatePayment(long orderId, CancellationToken cancellationToken)
    {
        var customerId = await _orderService.GetCustomerIdForOrderAsync(orderId);
        if (customerId is null)
            return NotFound(new { message = "Pesanan tidak ditemukan." });

        var result = await _paymentService.CreateOrGetPaymentAsync(orderId, customerId.Value, cancellationToken);
        if (result.Payment is null)
        {
            if (result.NotFound)
                return NotFound(new { message = result.Error });

            if (result.InProgress)
                return Conflict(new { message = result.Error });

            if (result.ProviderUnavailable)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = result.Error });

            return Conflict(new { message = result.Error });
        }

        return Ok(new
        {
            id = result.Payment.Id,
            orderId = result.Payment.OrderId,
            grossAmount = result.Payment.GrossAmount,
            status = result.Payment.Status,
            snapToken = result.Payment.SnapToken,
            redirectUrl = result.Payment.RedirectUrl,
            expiresAt = result.Payment.ExpiresAt
        });
    }

    private long? GetStaffId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return claim != null && long.TryParse(claim.Value, out var id) ? id : null;
    }
}
