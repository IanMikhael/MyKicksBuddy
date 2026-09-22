using Microsoft.AspNetCore.Identity;
using MySqlConnector;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Repositories;

namespace MyKicksBuddy.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IAddressRepository _addressRepository;
    private readonly IUserRepository _userRepository;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public OrderService(IOrderRepository orderRepository, IAddressRepository addressRepository, IUserRepository userRepository)
    {
        _orderRepository = orderRepository;
        _addressRepository = addressRepository;
        _userRepository = userRepository;
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

        var order = new Order
        {
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

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            order.OrderCode = GenerateOrderCode();
            try
            {
                var orderId = await _orderRepository.CreateWithItemsAsync(order, request.Items);
                return (true, null, orderId);
            }
            catch (MySqlException ex) when (ex.Number == 1062 && ex.Message.Contains("order_code") && attempt < maxAttempts)
            {
                // Tabrakan kode pesanan (kemungkinan sangat kecil) - coba lagi dengan kode baru.
            }
            catch (InvalidOperationException ex)
            {
                return (false, ex.Message, 0);
            }
        }

        return (false, "Gagal membuat kode pesanan yang unik. Silakan coba lagi.", 0);
    }

    private static string GenerateOrderCode() =>
        $"MKC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..7].ToUpper()}";

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
        if (!OrderStatusWorkflow.IsKnownStatus(status))
            throw new ArgumentException($"Status '{status}' tidak valid.");

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order is null)
            throw new KeyNotFoundException("Pesanan tidak ditemukan.");

        if (!OrderStatusWorkflow.CanTransition(order.Status, order.PaymentStatus, status))
            throw new InvalidOperationException(
                $"Tidak bisa mengubah status dari '{order.Status}' ke '{status}' (payment_status: {order.PaymentStatus}).");

        await _orderRepository.UpdateStatusWithLogAsync(orderId, status, handledBy, notes);
    }

    public async Task<OrderDetailResponse?> GetOrderDetailForStaffAsync(long orderId)
    {
        return await _orderRepository.GetDetailByIdAsync(orderId);
    }

    // --- Implementasi Chatbot (Mengambil langsung dari Database) ---

    public async Task<OrderDetailResponse?> GetOrderByCodeAsync(string orderCode)
    {
        return await _orderRepository.GetDetailByCodeAsync(orderCode);
    }

    public async Task<IEnumerable<ServiceDto>> GetAllServicesAsync()
    {
        return await _orderRepository.GetAllServicesAsync();
    }

    public async Task<(bool Success, string? Error, string? OrderCode, decimal TotalAmount)> CreateOrderForChatbotAsync(CreateChatbotOrderRequest request)
    {
        var customer = await _userRepository.GetByEmailOrPhoneAsync(request.CustomerPhone);
        if (customer is null)
        {
            customer = new User
            {
                Role = "customer",
                FullName = request.CustomerName,
                Phone = request.CustomerPhone,
                IsActive = true
            };
            customer.PasswordHash = _passwordHasher.HashPassword(customer, Guid.NewGuid().ToString("N"));

            var newCustomerId = await _userRepository.CreateAsync(customer);
            customer.Id = newCustomerId;
        }

        decimal? distanceKm = null;

        if (request.FulfillmentType == "pickup_delivery")
        {
            if (!request.AddressId.HasValue)
                return (false, "Alamat penjemputan wajib dipilih untuk layanan antar-jemput.", null, 0);

            var address = await _addressRepository.GetByIdAsync(request.AddressId.Value);
            if (address is null || address.UserId != customer.Id)
                return (false, "Alamat tidak ditemukan atau bukan milik pelanggan ini.", null, 0);

            if (!address.IsWithinRadius)
                return (false, $"Maaf, alamat berada di luar jangkauan (jarak {address.DistanceKm} km dari toko, maksimal 5 km).", null, 0);

            distanceKm = (decimal)address.DistanceKm;
        }

        var order = new Order
        {
            CustomerId = customer.Id,
            Channel = "online",
            FulfillmentType = request.FulfillmentType,
            AddressId = request.FulfillmentType == "pickup_delivery" ? request.AddressId : null,
            DistanceKm = distanceKm,
            Subtotal = 0,
            Total = 0,
            Status = "pending_payment",
            PaymentStatus = "unpaid",
            Notes = request.Notes
        };

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            order.OrderCode = GenerateOrderCode();
            try
            {
                await _orderRepository.CreateWithItemsAsync(order, request.Items);
                return (true, null, order.OrderCode, order.Total);
            }
            catch (MySqlException ex) when (ex.Number == 1062 && ex.Message.Contains("order_code") && attempt < maxAttempts)
            {
                // Tabrakan kode pesanan (kemungkinan sangat kecil) - coba lagi dengan kode baru.
            }
            catch (InvalidOperationException ex)
            {
                return (false, ex.Message, null, 0);
            }
        }

        return (false, "Gagal membuat kode pesanan yang unik. Silakan coba lagi.", null, 0);
    }

    // --- Implementasi POS (Kasir) ---

    public async Task<(bool Success, string? Error, long OrderId, long CustomerId)> CreatePosOrderAsync(long staffId, CreatePosOrderRequest request)
    {
        var customer = await _userRepository.GetByEmailOrPhoneAsync(request.CustomerPhone);
        if (customer is null)
        {
            customer = new User
            {
                Role = "customer",
                FullName = request.CustomerName,
                Phone = request.CustomerPhone,
                IsActive = true
            };
            customer.PasswordHash = _passwordHasher.HashPassword(customer, Guid.NewGuid().ToString("N"));

            var newCustomerId = await _userRepository.CreateAsync(customer);
            customer.Id = newCustomerId;
        }

        string orderCode = $"MKC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

        var order = new Order
        {
            OrderCode = orderCode,
            CustomerId = customer.Id,
            Channel = "pos",
            FulfillmentType = "drop_off",
            AddressId = null,
            DistanceKm = null,
            Subtotal = 0,
            Total = 0,
            Status = "pending_payment",
            PaymentStatus = "unpaid",
            HandledBy = staffId,
            Notes = request.Notes
        };

        var orderId = await _orderRepository.CreateWithItemsAsync(order, request.Items);

        if (request.PaymentMethod == "cash")
        {
            var createdOrder = await _orderRepository.GetByIdAsync(orderId);
            await _orderRepository.MarkPaidCashAsync(orderId, staffId, createdOrder!.Total);
        }

        return (true, null, orderId, customer.Id);
    }

    public async Task<long?> GetCustomerIdForOrderAsync(long orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        return order?.CustomerId;
    }

    public async Task<IEnumerable<StaffOrderListResponse>> GetOrdersForStaffAsync(string? channel, string? status)
    {
        return await _orderRepository.GetAllForStaffAsync(channel, status);
    }
}