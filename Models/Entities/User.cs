namespace MyKicksBuddy.Models.Entities;

public class User
{
    public long Id { get; set; }
    public string Role { get; set; } = "customer"; // customer, kasir, admin
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string SecurityStamp { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}