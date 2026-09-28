using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Models.Dtos;

public sealed class StaffOrderRow
{
    public long Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? ServiceName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string FulfillmentType { get; set; } = string.Empty;
    public string? FullAddress { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class StaffPaymentRow
{
    public long OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? ServiceName { get; set; }
    public decimal TotalAmount { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ProviderReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

public sealed class StaffActivityRow
{
    public long OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class StaffDashboardCounts
{
    public int Incoming { get; set; }
    public int NeedsAction { get; set; }
    public int Pickup { get; set; }
    public int Paid { get; set; }
}

public sealed class StaffDashboardPageViewModel
{
    public StaffDashboardCounts Counts { get; init; } = new();
    public IReadOnlyList<StaffOrderRow> Attention { get; init; } = [];
    public IReadOnlyList<StaffOrderRow> Pickups { get; init; } = [];
    public IReadOnlyList<StaffPaymentRow> RecentPayments { get; init; } = [];
}

public sealed class StaffOrdersPageViewModel
{
    public StaffDashboardCounts Counts { get; init; } = new();
    public IReadOnlyList<StaffOrderRow> Orders { get; init; } = [];
    public string? Query { get; init; }
    public string? Status { get; init; }
    public string? PaymentStatus { get; init; }
    public int Page { get; init; } = 1;
    public int Total { get; init; }
    public bool IsPickup { get; init; }
    public bool IsSearch { get; init; }
    public OrderDetailResponse? SelectedDetail { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / 20d));
}

public sealed class StaffPaymentsPageViewModel
{
    public IReadOnlyList<StaffPaymentRow> Payments { get; init; } = [];
    public string? Query { get; init; }
    public string? Status { get; init; }
    public long? SelectedOrderId { get; init; }
    public int Page { get; init; } = 1;
    public int Total { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / 20d));
    public StaffPaymentRow? Selected => Payments.FirstOrDefault(x => x.OrderId == SelectedOrderId) ?? Payments.FirstOrDefault();
}

public sealed class StaffProfilePageViewModel
{
    public required User User { get; init; }
}

public sealed class StaffPosPageViewModel
{
    public string? Query { get; init; }
    public IReadOnlyList<StaffOrderRow> Results { get; init; } = [];
    public StaffPaymentRow? Selected { get; init; }
}
