namespace MyKicksBuddy.Models.Dtos;

public class ServiceOptionDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int? EstimatedDurationDays { get; set; }
}
