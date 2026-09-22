using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[ApiController]
[Route("payments/midtrans")]
[AllowAnonymous]
public sealed class MidtransNotificationController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public MidtransNotificationController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("notification")]
    public async Task<IActionResult> HandleNotification([FromBody] MidtransNotificationRequest notification)
    {
        var result = await _paymentService.ProcessNotificationAsync(notification);
        return result switch
        {
            PaymentNotificationResult.InvalidSignature => Unauthorized(new { message = "Signature tidak valid." }),
            PaymentNotificationResult.ProviderUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Status pembayaran belum dapat diverifikasi ke Midtrans. Silakan coba lagi." }),
            PaymentNotificationResult.InvalidAmount => BadRequest(new { message = "Nominal pembayaran tidak valid." }),
            PaymentNotificationResult.PaymentNotFound => NotFound(new { message = "Percobaan pembayaran tidak ditemukan." }),
            PaymentNotificationResult.AmountMismatch => BadRequest(new { message = "Nominal pembayaran tidak cocok." }),
            _ => Ok(new { message = "Notifikasi pembayaran diproses." })
        };
    }
}
