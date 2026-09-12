using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Repositories;

namespace MyKicksBuddy.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IAddressRepository _addressRepository;

    public OrderService(IOrderRepository orderRepository, IAddressRepository addressRepository)
    {
        _orderRepository = orderRepository;
        _addressRepository = addressRepository;
    }

    public async Task<(bool Success, string? Error, long OrderId)> CreateOrderAsync(long customerId, CreateOrderRequest request)
    {
        decimal? distanceKm = null;

        if (request.FulfillmentType == "pickup_delivery")
        {
            if (!request.AddressId.HasValue)
                return (false, "Alamat penjemputan wajib dipilih untuk layanan antar-jemput.", 0);

            var address = await _addressRepository.GetByIdAsync(request.AddressId.Value);
            if (address is null || address.UserId != customerId)
                return (false, "Alamat tidak ditemukan atau bukan milik pengguna.", 0);

            if (!address.IsWithinRadius)
                return (false, $"Maaf, alamat anda berada di luar jangkauan (jarak {address.DistanceKm} km dari toko, maksimal 5 km).", 0);

            distanceKm = (decimal)address.DistanceKm; 
        }

        string orderCode = $"MKC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

        var order = new Order
        {
            OrderCode = orderCode,
            CustomerId = customerId,
            Channel = "online",
            FulfillmentType = request.FulfillmentType,
            AddressId = request.AddressId,
            DistanceKm = distanceKm,
            Subtotal = 0,
            Total = 0,
            Status = "pending_payment",
            PaymentStatus = "unpaid",
            Notes = request.Notes
        };

        var orderId = await ((OrderRepository)_orderRepository).CreateWithItemsAsync(order, request.Items);
        
        return (true, null, orderId);
    }

    public async Task<(bool Success, string? Error)> UpdateStatusAsync(long orderId, string status, long staffId, string? notes)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order is null)
            return (false, "Pesanan tidak ditemukan.");

        var validStatuses = new[] { 
            "pending_payment", "confirmed", "picked_up", "in_progress", 
            "ready", "delivered", "completed", "cancelled" 
        };

        if (!validStatuses.Contains(status))
            return (false, "Status pesanan tidak valid.");

        await _orderRepository.UpdateStatusWithLogAsync(orderId, status, staffId, notes);
        return (true, null);
    }

    public async Task<IEnumerable<OrderStatusLogResponse>> GetOrderLogsAsync(long orderId)
    {
        return await _orderRepository.GetLogsByOrderIdAsync(orderId);
    }

    public async Task<IEnumerable<OrderResponse>> GetOrdersByCustomerAsync(long customerId)
    {
        return await _orderRepository.GetByCustomerIdAsync(customerId);
    }

    public async Task<OrderDetailResponse?> GetOrderDetailAsync(long orderId, long customerId)
    {
        return await _orderRepository.GetDetailByIdAndCustomerAsync(orderId, customerId);
    }

    public async Task UpdateOrderStatusWithLogAsync(long orderId, string status, long handledBy, string? notes)
    {
        await _orderRepository.UpdateStatusWithLogAsync(orderId, status, handledBy, notes);
    }

    public async Task<OrderDetailResponse?> GetOrderDetailForStaffAsync(long orderId)
    {
        return await _orderRepository.GetDetailByIdAsync(orderId);
    }

    // --- Implementasi Chatbot (Mengambil langsung dari Database) ---

    public async Task<object?> GetOrderByCodeAsync(string orderCode)
    {
        return await _orderRepository.GetDetailByCodeAsync(orderCode);
    }

    public async Task<object> GetAllServicesAsync()
    {
        // Mengambil daftar layanan aktif dari database (tabel services)
        return await _orderRepository.GetAllServicesAsync();
    }
}