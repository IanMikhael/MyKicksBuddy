namespace MyKicksBuddy.Models.Entities;

public class CustomerAddress
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string FullAddress { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceKm { get; set; }
    public bool IsWithinRadius { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}