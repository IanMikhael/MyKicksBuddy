using MyKicksBuddy.Models.Dtos;

namespace MyKicksBuddy.Services;

public interface IOrderService
{
    Task<(bool Success, string? Error, long OrderId)> CreateOrderAsync(long customerId, CreateOrderRequest request);

    Task<(bool Success, string? Error)> UpdateStatusAsync(long orderId, string status, long staffId, string? notes);

    Task<IEnumerable<OrderStatusLogResponse>> GetOrderLogsAsync(long orderId);

    Task<IEnumerable<OrderResponse>> GetOrdersByCustomerAsync(long customerId);

    Task<OrderDetailResponse?> GetOrderDetailAsync(long orderId, long customerId);

    Task UpdateOrderStatusWithLogAsync(long orderId, string status, long handledBy, string? notes);

    Task<OrderDetailResponse?> GetOrderDetailForStaffAsync(long orderId);

    // Tambahan untuk Chatbot:
    Task<OrderDetailResponse?> GetOrderByCodeAsync(string orderCode);
    Task<IEnumerable<ServiceDto>> GetAllServicesAsync();
    Task<(bool Success, string? Error, string? OrderCode, decimal TotalAmount)> CreateOrderForChatbotAsync(CreateChatbotOrderRequest request);
    Task<IReadOnlyList<ChatbotAddressResponse>> GetCustomerAddressesAsync(string phone);

    // Tambahan untuk POS (Kasir):
    Task<(bool Success, string? Error, long OrderId, long CustomerId)> CreatePosOrderAsync(long staffId, CreatePosOrderRequest request);
    Task<long?> GetCustomerIdForOrderAsync(long orderId);
    Task<IEnumerable<StaffOrderListResponse>> GetOrdersForStaffAsync(string? channel, string? status);
}