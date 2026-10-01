namespace MyKicksBuddy.Models.Dtos;

public sealed class PaymentPlaygroundRequest
{
    public decimal Amount { get; set; }
    public string Bank { get; set; } = "bca";
    public int ExpiryMinutes { get; set; } = 60;
}
