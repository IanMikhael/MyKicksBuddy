using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[ApiController]
[Route("orders/{orderId:long}/payments")]
[Authorize(Roles = "customer")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(long orderId, CancellationToken cancellationToken)
    {
        var customerId = GetCustomerId();
        if (customerId is null)
            return Unauthorized();

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

        return Ok(ToResponse(result.Payment));
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(long orderId)
    {
        var customerId = GetCustomerId();
        if (customerId is null)
            return Unauthorized();

        var payment = await _paymentService.GetLatestPaymentAsync(orderId, customerId.Value);
        return payment is null ? NotFound() : Ok(ToResponse(payment));
    }

    private long? GetCustomerId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
        return long.TryParse(value, out var id) ? id : null;
    }

    private static PaymentResponse ToResponse(MyKicksBuddy.Models.Entities.Payment payment) => new()
    {
        Id = payment.Id,
        OrderId = payment.OrderId,
        GrossAmount = payment.GrossAmount,
        Currency = payment.Currency,
        Status = payment.Status,
        RedirectUrl = payment.RedirectUrl,
        CreatedAt = payment.CreatedAt,
        ExpiresAt = payment.ExpiresAt,
        PaidAt = payment.PaidAt
    };
}
