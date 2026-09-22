namespace MyKicksBuddy.Services;

public sealed class PaymentOrderUnavailableException : Exception
{
    public PaymentOrderUnavailableException(string message) : base(message)
    {
    }
}
