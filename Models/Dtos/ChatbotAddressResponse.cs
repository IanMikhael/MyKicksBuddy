namespace MyKicksBuddy.Models.Dtos;

public class ChatbotAddressResponse
{
    public long Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string FullAddress { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public bool IsWithinRadius { get; set; }
    public bool IsDefault { get; set; }
}
