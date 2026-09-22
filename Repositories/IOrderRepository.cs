using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Models.Dtos;

namespace MyKicksBuddy.Repositories;

public interface IOrderRepository
{
    Task<long> CreateAsync(Order order);
    Task<long> CreateWithItemsAsync(Order order, IReadOnlyCollection<OrderItemRequest> items);
    Task<Order?> GetByIdAsync(long id);
    Task<IEnumerable<OrderResponse>> GetByCustomerIdAsync(long customerId);
    Task<bool> UpdateStatusWithLogAsync(long orderId, string expectedStatus, string status, long handledBy, string? notes);
    Task<IEnumerable<OrderStatusLogResponse>> GetLogsByOrderIdAsync(long orderId);
    Task<OrderDetailResponse?> GetDetailByIdAndCustomerAsync(long orderId, long customerId);
    Task<OrderDetailResponse?> GetDetailByIdAsync(long orderId);
    Task<OrderDetailResponse?> GetDetailByCodeAndCustomerAsync(string orderCode, long customerId);
    Task<IReadOnlyList<ServiceOptionDto>> GetActiveServicesAsync();
    Task<IReadOnlyList<PortalOrderRow>> GetAllAsync(string? status, DateTime? from, DateTime? to, int? limit = null);
    Task<DashboardSummary> GetDashboardSummaryAsync();
}
