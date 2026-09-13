using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Models.Dtos;

namespace MyKicksBuddy.Repositories;

public interface IOrderRepository
{
    Task<long> CreateAsync(Order order);
    Task<long> CreateWithItemsAsync(Order order, IEnumerable<OrderItemRequest> items);
    Task<Order?> GetByIdAsync(long id);
    Task<IEnumerable<OrderResponse>> GetByCustomerIdAsync(long customerId);
    Task UpdateStatusAsync(long id, string status, string paymentStatus);
    Task UpdateStatusWithLogAsync(long orderId, string status, long handledBy, string? notes);
    Task<IEnumerable<OrderStatusLogResponse>> GetLogsByOrderIdAsync(long orderId);
    Task<OrderDetailResponse?> GetDetailByIdAndCustomerAsync(long orderId, long customerId);
    Task<OrderDetailResponse?> GetDetailByIdAsync(long orderId);
    Task<OrderDetailResponse?> GetDetailByCodeAsync(string orderCode);
    Task<IEnumerable<ServiceDto>> GetAllServicesAsync();
}