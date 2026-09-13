namespace MyKicksBuddy.Models.Dtos;

public class ServiceDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int EstimatedHours { get; set; }
    public bool IsActive { get; set; }
}