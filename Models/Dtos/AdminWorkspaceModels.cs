using System.ComponentModel.DataAnnotations;

namespace MyKicksBuddy.Models.Dtos;

public sealed class AdminPaymentRow
{
    public long OrderId { get; set; }
    public string OrderCode { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string PaymentStatus { get; set; } = "";
    public string? Provider { get; set; }
    public string? ProviderReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public bool HasAttempt { get; set; }
}

public sealed class AdminEventRow
{
    public long OrderId { get; set; }
    public string OrderCode { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AdminDailyPoint
{
    public DateTime Day { get; set; }
    public int Orders { get; set; }
}

public sealed class AdminReportViewModel
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public int TotalOrders { get; init; }
    public int CompletedOrders { get; init; }
    public int InProcessOrders { get; init; }
    public int PaidOrders { get; init; }
    public int UnpaidOrders { get; init; }
    public int FailedOrders { get; init; }
    public int ExpiredOrders { get; init; }
    public decimal PaidRevenue { get; init; }
    public IReadOnlyList<AdminDailyPoint> DailyOrders { get; init; } = [];
}

public sealed class AdminProfileViewModel
{
    public long Id { get; init; }
    public string FullName { get; init; } = "";
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string Role { get; init; } = "";
}

public sealed class AdminProfileEditModel
{
    [Required, StringLength(255)]
    public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = "";
    [Phone, StringLength(30)]
    public string? Phone { get; set; }
}

public sealed class AdminPasswordChangeModel
{
    [Required] public string CurrentPassword { get; set; } = "";
    [Required, MinLength(8)] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}
