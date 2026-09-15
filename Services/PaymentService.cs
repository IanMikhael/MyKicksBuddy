using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Repositories;

namespace MyKicksBuddy.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IMidtransSnapClient _midtransClient;
    private readonly MidtransOptions _options;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IOrderRepository orderRepository,
        IPaymentRepository paymentRepository,
        IMidtransSnapClient midtransClient,
        IOptions<MidtransOptions> options,
        ILogger<PaymentService> logger)
    {
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _midtransClient = midtransClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PaymentInitiationResult> CreateOrGetPaymentAsync(
        long orderId,
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order is null || order.CustomerId != customerId)
            return new PaymentInitiationResult(null, "Pesanan tidak ditemukan.", NotFound: true);

        if (order.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase))
            return new PaymentInitiationResult(null, "Pesanan ini sudah dibayar.");

        if (!order.Status.Equals("pending_payment", StringComparison.OrdinalIgnoreCase))
            return new PaymentInitiationResult(null, "Pesanan tidak berada pada status yang dapat dibayar.");

        if (order.Total <= 0 || decimal.Truncate(order.Total) != order.Total)
            return new PaymentInitiationResult(null, "Total pembayaran harus berupa jumlah Rupiah bulat yang lebih besar dari nol.");

        if (_options.ExpiryMinutes <= 0)
            return new PaymentInitiationResult(null, "Masa berlaku pembayaran Midtrans harus lebih besar dari nol.");

        var providerOrderId = $"MKC{Guid.NewGuid():N}";
        Payment payment;
        bool created;
        try
        {
            (payment, created) = await _paymentRepository.GetOrCreateActiveAsync(
                order.Id,
                providerOrderId,
                order.Total);
        }
        catch (PaymentOrderUnavailableException exception)
        {
            return new PaymentInitiationResult(null, exception.Message);
        }

        if (!created)
        {
            if (payment.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
            {
                if (!payment.IsExpired && !string.IsNullOrWhiteSpace(payment.RedirectUrl))
                    return new PaymentInitiationResult(payment, null);

                var reconciledPayment = await GetLatestPaymentAsync(orderId, customerId);
                if (reconciledPayment?.Status == "paid")
                    return new PaymentInitiationResult(null, "Pesanan ini sudah dibayar.");

                if (reconciledPayment?.Status is "expired" or "failed" or "cancelled")
                {
                    order = await _orderRepository.GetByIdAsync(orderId);
                    if (order is null || order.CustomerId != customerId)
                        return new PaymentInitiationResult(null, "Pesanan tidak ditemukan.", NotFound: true);

                    if (order.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase) ||
                        !order.Status.Equals("pending_payment", StringComparison.OrdinalIgnoreCase))
                        return new PaymentInitiationResult(null, "Pesanan tidak lagi menunggu pembayaran.");

                    try
                    {
                        (payment, created) = await _paymentRepository.GetOrCreateActiveAsync(
                            order.Id,
                            $"MKC{Guid.NewGuid():N}",
                            order.Total);
                    }
                    catch (PaymentOrderUnavailableException exception)
                    {
                        return new PaymentInitiationResult(null, exception.Message);
                    }
                }

                if (!created)
                    return new PaymentInitiationResult(null, "Status pembayaran sedang dikonfirmasi Midtrans. Coba lagi sebentar lagi.", InProgress: true);
            }
            else
            {
                return new PaymentInitiationResult(null, "Sesi pembayaran sedang dibuat. Coba periksa kembali sebentar lagi.", InProgress: true);
            }

        }

        try
        {
            var snap = await _midtransClient.CreateTransactionAsync(
                payment.ProviderOrderId,
                decimal.ToInt64(payment.GrossAmount),
                _options.ExpiryMinutes,
                cancellationToken);

            await _paymentRepository.SaveSnapSessionAsync(payment.Id, snap.Token, snap.RedirectUrl, _options.ExpiryMinutes);
            payment.SnapToken = snap.Token;
            payment.RedirectUrl = snap.RedirectUrl;
            payment.Status = "pending";
            payment.ExpiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);
            return new PaymentInitiationResult(payment, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
        {
            await _paymentRepository.MarkInitiationFailedAsync(payment.Id);
            _logger.LogError(exception, "Gagal membuat sesi Snap untuk order {OrderId}.", order.Id);
            return new PaymentInitiationResult(
                null,
                "Sesi pembayaran gagal dibuat. Silakan coba lagi.",
                ProviderUnavailable: true);
        }
    }

    public async Task<Payment?> GetLatestPaymentAsync(long orderId, long customerId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order is null || order.CustomerId != customerId)
            return null;

        var payment = await _paymentRepository.GetLatestByOrderIdAsync(orderId);
        if (payment is null || payment.Status is not ("pending" or "challenge"))
            return payment;

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var currentStatus = await _midtransClient.GetTransactionStatusAsync(payment.ProviderOrderId, timeout.Token);
            if (!string.Equals(currentStatus.OrderId, payment.ProviderOrderId, StringComparison.Ordinal) ||
                !decimal.TryParse(currentStatus.GrossAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var grossAmount) ||
                grossAmount != payment.GrossAmount)
                return payment;

            var mappedStatus = MapTransactionStatus(currentStatus);
            if (mappedStatus is not null)
            {
                await _paymentRepository.ApplyNotificationAsync(
                    currentStatus.OrderId,
                    grossAmount,
                    mappedStatus,
                    currentStatus.TransactionStatus,
                    currentStatus.StatusCode,
                    currentStatus.TransactionId,
                    currentStatus.PaymentType,
                    currentStatus.FraudStatus);
                payment = await _paymentRepository.GetLatestByOrderIdAsync(orderId) ?? payment;
            }
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // A Snap transaction may not have a Core API status until a payment method is selected.
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(exception, "Gagal menyinkronkan status pembayaran {PaymentId}.", payment.Id);
        }

        return payment;
    }

    public async Task<PaymentNotificationResult> ProcessNotificationAsync(MidtransNotificationRequest notification)
    {
        if (!IsSignatureValid(notification))
            return PaymentNotificationResult.InvalidSignature;

        MidtransTransactionStatusResponse currentStatus;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            currentStatus = await _midtransClient.GetTransactionStatusAsync(notification.OrderId, timeout.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(exception, "Gagal memverifikasi status transaksi Midtrans {ProviderOrderId}.", notification.OrderId);
            return PaymentNotificationResult.ProviderUnavailable;
        }

        if (!string.Equals(currentStatus.OrderId, notification.OrderId, StringComparison.Ordinal))
            return PaymentNotificationResult.InvalidSignature;

        if (!decimal.TryParse(notification.GrossAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var notificationAmount) ||
            !decimal.TryParse(currentStatus.GrossAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var grossAmount))
            return PaymentNotificationResult.InvalidAmount;

        if (notificationAmount != grossAmount)
            return PaymentNotificationResult.AmountMismatch;

        var mappedStatus = MapTransactionStatus(currentStatus);
        if (mappedStatus is null)
            return PaymentNotificationResult.Ignored;

        var result = await _paymentRepository.ApplyNotificationAsync(
            currentStatus.OrderId,
            grossAmount,
            mappedStatus,
            currentStatus.TransactionStatus,
            currentStatus.StatusCode,
            currentStatus.TransactionId,
            currentStatus.PaymentType,
            currentStatus.FraudStatus);

        return result switch
        {
            PaymentNotificationUpdateResult.Applied => PaymentNotificationResult.Applied,
            PaymentNotificationUpdateResult.Ignored => PaymentNotificationResult.Ignored,
            PaymentNotificationUpdateResult.PaymentNotFound => PaymentNotificationResult.PaymentNotFound,
            PaymentNotificationUpdateResult.AmountMismatch => PaymentNotificationResult.AmountMismatch,
            _ => PaymentNotificationResult.Ignored
        };
    }

    private bool IsSignatureValid(MidtransNotificationRequest notification)
    {
        if (string.IsNullOrWhiteSpace(_options.ServerKey) ||
            string.IsNullOrWhiteSpace(notification.OrderId) ||
            string.IsNullOrWhiteSpace(notification.StatusCode) ||
            string.IsNullOrWhiteSpace(notification.GrossAmount) ||
            string.IsNullOrWhiteSpace(notification.SignatureKey))
            return false;

        var signatureInput = string.Concat(
            notification.OrderId,
            notification.StatusCode,
            notification.GrossAmount,
            _options.ServerKey);
        var expectedSignature = SHA512.HashData(Encoding.UTF8.GetBytes(signatureInput));

        try
        {
            var suppliedSignature = Convert.FromHexString(notification.SignatureKey);
            return suppliedSignature.Length == expectedSignature.Length &&
                   CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? MapTransactionStatus(MidtransTransactionStatusResponse transaction)
    {
        var transactionStatus = transaction.TransactionStatus.Trim().ToLowerInvariant();
        var fraudStatus = transaction.FraudStatus?.Trim();
        var fraudAccepted = string.IsNullOrWhiteSpace(fraudStatus) ||
                            fraudStatus.Equals("accept", StringComparison.OrdinalIgnoreCase);

        return transactionStatus switch
        {
            "settlement" when transaction.StatusCode == "200" && fraudAccepted => "paid",
            "capture" when transaction.StatusCode == "200" && fraudAccepted => "paid",
            "capture" when fraudStatus?.Equals("challenge", StringComparison.OrdinalIgnoreCase) == true => "challenge",
            "capture" when fraudStatus?.Equals("deny", StringComparison.OrdinalIgnoreCase) == true => "failed",
            "pending" => "pending",
            "deny" or "failure" => "failed",
            "expire" => "expired",
            "cancel" => "cancelled",
            "refund" => "refunded",
            "partial_refund" => "partially_refunded",
            _ => null
        };
    }
}
