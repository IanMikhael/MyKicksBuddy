namespace MyKicksBuddy.Services;

public interface IMidtransSnapClient
{
    Task<SnapTransactionResponse> CreateTransactionAsync(string providerOrderId, long grossAmount, int expiryMinutes, CancellationToken cancellationToken = default);
    Task<MidtransTransactionStatusResponse> GetTransactionStatusAsync(string providerOrderId, CancellationToken cancellationToken = default);
}

public sealed record SnapTransactionResponse(string Token, string RedirectUrl);

public sealed record MidtransTransactionStatusResponse(
    string OrderId,
    string StatusCode,
    string GrossAmount,
    string TransactionStatus,
    string? TransactionId,
    string? PaymentType,
    string? FraudStatus);
