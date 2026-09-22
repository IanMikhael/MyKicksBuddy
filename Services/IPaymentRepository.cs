using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Services;

public interface IPaymentRepository
{
    Task<(Payment Payment, bool Created)> GetOrCreateActiveAsync(long orderId, string providerOrderId, decimal grossAmount);
    Task<Payment?> GetLatestByOrderIdAsync(long orderId);
    Task SaveSnapSessionAsync(long paymentId, string token, string redirectUrl, int expiryMinutes);
    Task MarkInitiationFailedAsync(long paymentId);
    Task<PaymentNotificationUpdateResult> ApplyNotificationAsync(
        string providerOrderId,
        decimal grossAmount,
        string status,
        string providerStatus,
        string statusCode,
        string? transactionId,
        string? paymentType,
        string? fraudStatus);
}

public enum PaymentNotificationUpdateResult
{
    Applied,
    Ignored,
    PaymentNotFound,
    AmountMismatch
}
