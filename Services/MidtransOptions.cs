namespace MyKicksBuddy.Services;

public class MidtransOptions
{
    public const string SectionName = "Midtrans";

    public string ServerKey { get; set; } = string.Empty;
    public bool IsProduction { get; set; }
    public int ExpiryMinutes { get; set; } = 1440;
}
