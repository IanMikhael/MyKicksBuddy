namespace MyKicksBuddy.Services;

public interface IMidtransCoreClient
{
    Task<CoreChargeResponse> ChargeBankTransferAsync(
        string orderId,
        long grossAmount,
        string bank,
        CancellationToken cancellationToken = default);

    Task<CoreChargeResponse> ChargeQrisAsync(
        string orderId,
        long grossAmount,
        CancellationToken cancellationToken = default);

    Task<CoreChargeResponse> ChargeGoPayAsync(
        string orderId,
        long grossAmount,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string ContentType)> GetQrCodeAsync(
        string transactionId,
        string paymentType,
        CancellationToken cancellationToken = default);

    Task<CoreTransactionStatusResponse> GetTransactionStatusAsync(
        string orderId,
        CancellationToken cancellationToken = default);

    Task<CoreCancelResponse> CancelTransactionAsync(
        string orderId,
        CancellationToken cancellationToken = default);
}

public sealed record CoreChargeResponse(
    string OrderId,
    string StatusCode,
    string StatusMessage,
    string TransactionStatus,
    string? TransactionId,
    string? PaymentType,
    string? FraudStatus,
    string? Bank,
    string? VirtualAccount,
    IReadOnlyList<CoreAction> Actions);

public sealed record CoreAction(string Name, string Url);

public sealed record CoreTransactionStatusResponse(
    string OrderId,
    string StatusCode,
    string StatusMessage,
    string TransactionStatus,
    string GrossAmount,
    string? TransactionId,
    string? PaymentType,
    string? FraudStatus);

public sealed record CoreCancelResponse(
    string OrderId,
    string StatusCode,
    string StatusMessage,
    string TransactionStatus);
