namespace MyKicksBuddy.Models.Dtos;

public record OrderCreationResult(bool Success, string? Error, long OrderId, string? OrderCode)
{
    public static OrderCreationResult Failed(string error) => new(false, error, 0, null);
}
