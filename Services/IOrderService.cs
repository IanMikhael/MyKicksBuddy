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

    // Customer portal
    Task<OrderDetailResponse?> GetOrderByCodeAsync(string orderCode, long customerId);
    Task<IReadOnlyList<ServiceOptionDto>> GetAllServicesAsync();
    Task<IReadOnlyList<PortalOrderRow>> GetAllOrdersAsync(string? status, DateTime? from, DateTime? to, int? limit = null);
    Task<DashboardSummary> GetDashboardSummaryAsync();

    // API integration / POS
    Task<OrderDetailResponse?> GetOrderByCodeAsync(string orderCode);
    Task<IEnumerable<ServiceDto>> GetAllServiceDtosAsync();
    Task<(bool Success, string? Error, string? OrderCode, decimal TotalAmount)> CreateOrderForChatbotAsync(CreateChatbotOrderRequest request);
    Task<(bool Success, string? Error, long OrderId, long CustomerId)> CreatePosOrderAsync(long staffId, CreatePosOrderRequest request);
    Task<long?> GetCustomerIdForOrderAsync(long orderId);
    Task<IEnumerable<StaffOrderListResponse>> GetOrdersForStaffAsync(string? channel, string? status);
}
