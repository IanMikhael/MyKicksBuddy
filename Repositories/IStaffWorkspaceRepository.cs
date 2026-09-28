using MyKicksBuddy.Models.Dtos;

namespace MyKicksBuddy.Repositories;

public interface IStaffWorkspaceRepository
{
    Task<StaffDashboardCounts> GetCountsAsync();
    Task<(IReadOnlyList<StaffOrderRow> Rows, int Total)> GetOrdersAsync(string? query, string? status, string? paymentStatus, bool pickupOnly, int page, int pageSize);
    Task<(IReadOnlyList<StaffPaymentRow> Rows, int Total)> GetPaymentsAsync(string? query, string? status, int page, int pageSize);
    Task<StaffPaymentRow?> GetPaymentAsync(long orderId);
    Task<IReadOnlyList<StaffActivityRow>> GetActivityAsync(int limit);
}
