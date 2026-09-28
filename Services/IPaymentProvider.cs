namespace MyKicksBuddy.Services;

public sealed record PaymentCreation(string Provider, string ProviderReference);
public sealed record VerifiedPaymentResult(string Provider, string ProviderReference, decimal Amount, string Currency, string Status);

// A future adapter must use the provider's real idempotency and verification
// mechanisms. Never derive VerifiedPaymentResult from untrusted request fields.
public interface IPaymentProvider
{
    Task<PaymentCreation> CreateAsync(string orderCode, decimal amount, string currency, string idempotencyKey);
    Task<VerifiedPaymentResult?> VerifyNotificationAsync(string rawBody, IReadOnlyDictionary<string, string> headers);
}
