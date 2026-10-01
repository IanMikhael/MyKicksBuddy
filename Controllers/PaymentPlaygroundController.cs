using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[ApiController]
[Route("api/payment-playground")]
public sealed class PaymentPlaygroundController : ControllerBase
{
    private static readonly HashSet<string> SupportedBanks = ["bca", "bni", "bri", "cimb", "permata"];
    private readonly IHostEnvironment _environment;
    private readonly IMidtransSnapClient _snapClient;
    private readonly IMidtransCoreClient _coreClient;

    public PaymentPlaygroundController(
        IHostEnvironment environment,
        IMidtransSnapClient snapClient,
        IMidtransCoreClient coreClient)
    {
        _environment = environment;
        _snapClient = snapClient;
        _coreClient = coreClient;
    }

    [HttpPost("snap")]
    public async Task<IActionResult> CreateSnap([FromBody] PaymentPlaygroundRequest request, CancellationToken cancellationToken)
    {
        if (!IsDevelopment()) return NotFound();
        if (!TryValidate(request, out var amount, out var error)) return BadRequest(new { message = error });

        var orderId = $"MKC-UI-SNAP-{Guid.NewGuid():N}"[..30];
        try
        {
            var result = await _snapClient.CreateTransactionAsync(orderId, amount, request.ExpiryMinutes, cancellationToken);
            return Ok(new { provider = "snap", orderId, status = "pending", token = result.Token, redirectUrl = result.RedirectUrl });
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return StatusCode(502, new { message = exception.Message });
        }
    }

    [HttpPost("core")]
    public async Task<IActionResult> CreateCore([FromBody] PaymentPlaygroundRequest request, CancellationToken cancellationToken)
    {
        if (!IsDevelopment()) return NotFound();
        if (!TryValidate(request, out var amount, out var error)) return BadRequest(new { message = error });
        if (!SupportedBanks.Contains(request.Bank.Trim().ToLowerInvariant()))
            return BadRequest(new { message = "Bank Core API yang tersedia: BCA, BNI, BRI, CIMB, atau Permata." });

        var orderId = $"MKC-UI-CORE-{Guid.NewGuid():N}"[..30];
        try
        {
            var result = await _coreClient.ChargeBankTransferAsync(orderId, amount, request.Bank.Trim().ToLowerInvariant(), cancellationToken);
            return Ok(new
            {
                provider = "core",
                orderId,
                status = result.TransactionStatus,
                statusCode = result.StatusCode,
                statusMessage = result.StatusMessage,
                transactionId = result.TransactionId,
                paymentType = result.PaymentType,
                bank = result.Bank,
                virtualAccount = result.VirtualAccount
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return StatusCode(502, new { message = exception.Message });
        }
    }

    [HttpPost("qris")]
    public async Task<IActionResult> CreateQris([FromBody] PaymentPlaygroundRequest request, CancellationToken cancellationToken)
    {
        if (!IsDevelopment()) return NotFound();
        if (!TryValidate(request, out var amount, out var error)) return BadRequest(new { message = error });

        var orderId = $"MKC-UI-QRIS-{Guid.NewGuid():N}"[..30];
        try
        {
            var result = await _coreClient.ChargeQrisAsync(orderId, amount, cancellationToken);
            return Ok(ToPaymentResponse("qris", result));
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return StatusCode(502, new { message = exception.Message });
        }
    }

    [HttpPost("gopay")]
    public async Task<IActionResult> CreateGoPay([FromBody] PaymentPlaygroundRequest request, CancellationToken cancellationToken)
    {
        if (!IsDevelopment()) return NotFound();
        if (!TryValidate(request, out var amount, out var error)) return BadRequest(new { message = error });

        var orderId = $"MKC-UI-GOPAY-{Guid.NewGuid():N}"[..30];
        try
        {
            var result = await _coreClient.ChargeGoPayAsync(orderId, amount, cancellationToken);
            return Ok(ToPaymentResponse("gopay", result));
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return StatusCode(502, new { message = exception.Message });
        }
    }

    [HttpGet("qr-code/{transactionId}")]
    public async Task<IActionResult> GetQrCode(string transactionId, [FromQuery] string paymentType, CancellationToken cancellationToken)
    {
        if (!IsDevelopment()) return NotFound();
        try
        {
            var result = await _coreClient.GetQrCodeAsync(transactionId, paymentType, cancellationToken);
            return File(result.Content, result.ContentType);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or ArgumentException or TaskCanceledException)
        {
            return StatusCode(502, new { message = exception.Message });
        }
    }

    [HttpGet("{orderId}/status")]
    public async Task<IActionResult> GetStatus(string orderId, CancellationToken cancellationToken)
    {
        if (!IsDevelopment()) return NotFound();
        try
        {
            var result = await _coreClient.GetTransactionStatusAsync(orderId, cancellationToken);
            return Ok(new
            {
                orderId = result.OrderId,
                status = result.TransactionStatus,
                statusCode = result.StatusCode,
                statusMessage = result.StatusMessage,
                grossAmount = result.GrossAmount,
                transactionId = result.TransactionId,
                paymentType = result.PaymentType,
                fraudStatus = result.FraudStatus
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return StatusCode(502, new { message = exception.Message });
        }
    }

    [HttpPost("{orderId}/cancel")]
    public async Task<IActionResult> Cancel(string orderId, CancellationToken cancellationToken)
    {
        if (!IsDevelopment()) return NotFound();
        try
        {
            var result = await _coreClient.CancelTransactionAsync(orderId, cancellationToken);
            return Ok(new { orderId = result.OrderId, status = result.TransactionStatus, statusCode = result.StatusCode, message = result.StatusMessage });
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return StatusCode(502, new { message = exception.Message });
        }
    }

    private bool IsDevelopment() => _environment.IsDevelopment();

    private static object ToPaymentResponse(string provider, CoreChargeResponse result) => new
    {
        provider,
        orderId = result.OrderId,
        status = result.TransactionStatus,
        statusCode = result.StatusCode,
        statusMessage = result.StatusMessage,
        transactionId = result.TransactionId,
        paymentType = result.PaymentType,
        bank = result.Bank,
        virtualAccount = result.VirtualAccount,
        actions = result.Actions
    };

    private static bool TryValidate(PaymentPlaygroundRequest request, out long amount, out string? error)
    {
        amount = 0;
        error = null;
        if (request.Amount <= 0 || decimal.Truncate(request.Amount) != request.Amount || request.Amount > 10_000_000)
        {
            error = "Nominal harus berupa Rupiah bulat antara 1 dan 10.000.000.";
            return false;
        }

        if (request.ExpiryMinutes is < 1 or > 1440)
        {
            error = "Masa berlaku harus antara 1 dan 1440 menit.";
            return false;
        }

        amount = decimal.ToInt64(request.Amount);
        return true;
    }
}
