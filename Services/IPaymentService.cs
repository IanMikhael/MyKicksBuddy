using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Services;

public interface IPaymentService
{
    Task<PaymentInitiationResult> CreateOrGetPaymentAsync(long orderId, long customerId, CancellationToken cancellationToken = default);
    Task<Payment?> GetLatestPaymentAsync(long orderId, long customerId);
    Task<PaymentNotificationResult> ProcessNotificationAsync(MidtransNotificationRequest notification);
}

public sealed record PaymentInitiationResult(
    Payment? Payment,
    string? Error,
    bool NotFound = false,
    bool InProgress = false,
    bool ProviderUnavailable = false);

public enum PaymentNotificationResult
{
    Applied,
    Ignored,
    InvalidSignature,
    ProviderUnavailable,
    InvalidAmount,
    PaymentNotFound,
    AmountMismatch
}
