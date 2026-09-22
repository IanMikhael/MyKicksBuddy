using MyKicksBuddy.Models.Dtos;

namespace MyKicksBuddy.Services;

public interface IOrderService
{
    Task<OrderCreationResult> CreateOrderAsync(long customerId, CreateOrderRequest request);

    Task<(bool Success, string? Error)> UpdateStatusAsync(long orderId, string status, long staffId, string staffRole, string? notes);

    Task<IEnumerable<OrderStatusLogResponse>> GetOrderLogsAsync(long orderId);

    Task<IEnumerable<OrderResponse>> GetOrdersByCustomerAsync(long customerId);

    Task<OrderDetailResponse?> GetOrderDetailAsync(long orderId, long customerId);

    Task UpdateOrderStatusWithLogAsync(long orderId, string status, long handledBy, string? notes);

    Task<OrderDetailResponse?> GetOrderDetailForStaffAsync(long orderId);

    // Tambahan untuk Chatbot:
    Task<OrderDetailResponse?> GetOrderByCodeAsync(string orderCode, long customerId);
    Task<IReadOnlyList<ServiceOptionDto>> GetAllServicesAsync();
    Task<IReadOnlyList<PortalOrderRow>> GetAllOrdersAsync(string? status, DateTime? from, DateTime? to, int? limit = null);
    Task<DashboardSummary> GetDashboardSummaryAsync();
}
