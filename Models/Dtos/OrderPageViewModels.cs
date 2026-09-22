namespace MyKicksBuddy.Models.Dtos;

public sealed class OrderDetailPageViewModel
{
    public required OrderDetailResponse Order { get; init; }
    public IReadOnlyList<OrderStatusLogResponse> Logs { get; init; } = [];
    public IReadOnlyList<string> AllowedStatuses { get; init; } = [];
}

public sealed class PortalOrderRow
{
    public long Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string FulfillmentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class OrdersPageViewModel
{
    public IReadOnlyList<PortalOrderRow> Orders { get; init; } = [];
    public string? Status { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? Search { get; init; }
    public string? PaymentStatus { get; init; }
    public int Page { get; init; } = 1;
    public int TotalCount { get; init; }
}

public sealed class DashboardSummary
{
    public int WaitingApprovalOrders { get; set; }
    public int PaidOrders { get; set; }
    public int TotalOrders { get; set; }
    public int ActiveOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int PendingPayments { get; set; }
    public decimal PaidRevenue { get; set; }
}

public sealed class AdminDashboardViewModel
{
    public DashboardSummary Summary { get; init; } = new();
    public IReadOnlyList<PortalOrderRow> RecentOrders { get; init; } = [];
    public IReadOnlyList<PortalOrderRow> PriorityOrders { get; init; } = [];
}

public sealed class ServiceAdminDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int? EstimatedDurationDays { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ServiceAdminFormModel
{
    public long Id { get; set; }
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Nama layanan wajib diisi.")]
    [System.ComponentModel.DataAnnotations.StringLength(255)]
    public string Name { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.StringLength(2000)]
    public string? Description { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0.01, 100000000, ErrorMessage = "Harga harus lebih dari 0.")]
    public decimal Price { get; set; }
    [System.ComponentModel.DataAnnotations.Range(1, 365)]
    public int? EstimatedDurationDays { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class InternalUserDto
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
