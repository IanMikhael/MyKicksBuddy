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

    // Kept for the focused lifecycle test harness; production DI resolves the constructor above.
    public OrderService(IOrderRepository orderRepository, IAddressRepository addressRepository)
    {
        _orderRepository = orderRepository;
        _addressRepository = addressRepository;
        _userRepository = null!;
    }

    public async Task<OrderCreationResult> CreateOrderAsync(long customerId, CreateOrderRequest request)
    {
        if (customerId <= 0)
            return OrderCreationResult.Failed("Customer tidak valid.");

        var fulfillmentType = request.FulfillmentType?.Trim().ToLowerInvariant();
        if (fulfillmentType is not ("pickup_delivery" or "drop_off"))
            return OrderCreationResult.Failed("Metode penyerahan pesanan tidak valid.");

        if (request.Items is null || request.Items.Count == 0)
            return OrderCreationResult.Failed("Pilih minimal satu layanan.");

        if (request.Items.Any(item => item.ServiceId <= 0 || item.Quantity is < 1 or > 20 || string.IsNullOrWhiteSpace(item.ShoeDescription)))
            return OrderCreationResult.Failed("Detail layanan, sepatu, atau jumlah belum valid.");

        decimal? distanceKm = null;

        if (fulfillmentType == "pickup_delivery")
        {
            if (!request.AddressId.HasValue)
                return OrderCreationResult.Failed("Alamat penjemputan wajib dipilih untuk layanan antar-jemput.");

            var address = await _addressRepository.GetByIdAsync(request.AddressId.Value, customerId);
            if (address is null)
                return OrderCreationResult.Failed("Alamat tidak ditemukan atau bukan milik Anda.");

            if (!address.IsWithinRadius)
                return OrderCreationResult.Failed($"Alamat berada di luar radius antar-jemput 5 km (jarak {address.DistanceKm:0.##} km). Pilih drop off untuk melanjutkan.");

            distanceKm = (decimal)address.DistanceKm;
        }
        else
        {
            request.AddressId = null;
        }

        var order = new Order
        {
            CustomerId = customerId,
            Channel = "online",
            FulfillmentType = fulfillmentType,
            AddressId = request.AddressId,
            DistanceKm = distanceKm,
            Subtotal = 0,
            Total = 0,
            Status = OrderStatusWorkflow.PendingPayment,
            PaymentStatus = PaymentStatusWorkflow.Unpaid,
            Notes = request.Notes
        };

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            order.OrderCode = GenerateOrderCode();
            try
            {
                var orderId = await _orderRepository.CreateWithItemsAsync(order, request.Items);
                return new OrderCreationResult(true, null, orderId, order.OrderCode);
            }
            catch (MySqlException ex) when (ex.Number == 1062 && ex.Message.Contains("order_code") && attempt < 3)
            {
            }
            catch (InvalidOperationException ex)
            {
                return OrderCreationResult.Failed(ex.Message);
            }
        }

        return OrderCreationResult.Failed("Gagal membuat kode pesanan yang unik. Silakan coba lagi.");
    }

    public async Task<(bool Success, string? Error)> UpdateStatusAsync(long orderId, string status, long staffId, string staffRole, string? notes)
    {
        if (staffId <= 0 || staffRole is not ("kasir" or "admin" or "staff"))
            return (false, "Petugas tidak memiliki izin untuk memperbarui status.");

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order is null)
            return (false, "Pesanan tidak ditemukan.");

        status = status?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!OrderStatusWorkflow.IsKnown(status))
            return (false, "Status pesanan tidak valid.");

        if (!OrderStatusWorkflow.OperatorNextStatuses(order.Status, order.PaymentStatus).Contains(status))
            return (false, "Transisi status tidak diizinkan untuk kondisi pesanan dan pembayaran saat ini.");

        if (!await _orderRepository.UpdateStatusWithLogAsync(orderId, order.Status, status, staffId, notes))
            return (false, "Status pesanan atau pembayaran telah berubah. Muat ulang sebelum mencoba lagi.");
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
        var result = await UpdateStatusAsync(orderId, status, handledBy, "staff", notes);
        if (!result.Success)
            throw new InvalidOperationException(result.Error);
    }

    public async Task<OrderDetailResponse?> GetOrderDetailForStaffAsync(long orderId)
    {
        return await _orderRepository.GetDetailByIdAsync(orderId);
    }

    // --- Implementasi Chatbot (Mengambil langsung dari Database) ---

    public async Task<OrderDetailResponse?> GetOrderByCodeAsync(string orderCode, long customerId)
    {
        return await _orderRepository.GetDetailByCodeAndCustomerAsync(orderCode.Trim(), customerId);
    }

    public Task<IReadOnlyList<ServiceOptionDto>> GetAllServicesAsync()
    {
        return _orderRepository.GetActiveServicesAsync();
    }

    public Task<IReadOnlyList<PortalOrderRow>> GetAllOrdersAsync(string? status, DateTime? from, DateTime? to, int? limit = null) =>
        _orderRepository.GetAllAsync(status, from, to, limit);

    public Task<DashboardSummary> GetDashboardSummaryAsync() => _orderRepository.GetDashboardSummaryAsync();

    public Task<OrderDetailResponse?> GetOrderByCodeAsync(string orderCode) =>
        _orderRepository.GetDetailByCodeAsync(orderCode.Trim());

    public Task<IEnumerable<ServiceDto>> GetAllServiceDtosAsync() => _orderRepository.GetAllServicesAsync();

    public async Task<(bool Success, string? Error, string? OrderCode, decimal TotalAmount)> CreateOrderForChatbotAsync(CreateChatbotOrderRequest request)
    {
        var customer = await GetOrCreateCustomerAsync(request.CustomerName, request.CustomerPhone);
        decimal? distanceKm = null;
        if (request.FulfillmentType == "pickup_delivery")
        {
            if (!request.AddressId.HasValue) return (false, "Alamat penjemputan wajib dipilih untuk layanan antar-jemput.", null, 0);
            var address = await _addressRepository.GetByIdAsync(request.AddressId.Value, customer.Id);
            if (address is null) return (false, "Alamat tidak ditemukan atau bukan milik pelanggan ini.", null, 0);
            if (!address.IsWithinRadius) return (false, "Alamat berada di luar jangkauan layanan.", null, 0);
            distanceKm = (decimal)address.DistanceKm;
        }

        var order = new Order
        {
            CustomerId = customer.Id, Channel = "online", FulfillmentType = request.FulfillmentType,
            AddressId = request.FulfillmentType == "pickup_delivery" ? request.AddressId : null,
            DistanceKm = distanceKm, Status = OrderStatusWorkflow.PendingPayment,
            PaymentStatus = PaymentStatusWorkflow.Unpaid, Notes = request.Notes
        };
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            order.OrderCode = GenerateOrderCode();
            try
            {
                await _orderRepository.CreateWithItemsAsync(order, request.Items);
                return (true, null, order.OrderCode, order.Total);
            }
            catch (MySqlException ex) when (ex.Number == 1062 && ex.Message.Contains("order_code") && attempt < 3) { }
            catch (InvalidOperationException ex) { return (false, ex.Message, null, 0); }
        }
        return (false, "Gagal membuat kode pesanan yang unik. Silakan coba lagi.", null, 0);
    }

    public async Task<(bool Success, string? Error, long OrderId, long CustomerId)> CreatePosOrderAsync(long staffId, CreatePosOrderRequest request)
    {
        var customer = await GetOrCreateCustomerAsync(request.CustomerName, request.CustomerPhone);
        var order = new Order
        {
            CustomerId = customer.Id, Channel = "pos", FulfillmentType = "drop_off",
            Status = OrderStatusWorkflow.PendingPayment, PaymentStatus = PaymentStatusWorkflow.Unpaid,
            HandledBy = staffId, Notes = request.Notes
        };
        long orderId = 0;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            order.OrderCode = GenerateOrderCode();
            try { orderId = await _orderRepository.CreateWithItemsAsync(order, request.Items); break; }
            catch (MySqlException ex) when (ex.Number == 1062 && ex.Message.Contains("order_code") && attempt < 3) { }
            catch (InvalidOperationException ex) { return (false, ex.Message, 0, customer.Id); }
        }
        if (orderId == 0) return (false, "Gagal membuat kode pesanan yang unik. Silakan coba lagi.", 0, customer.Id);
        if (request.PaymentMethod == "cash") await _orderRepository.MarkPaidCashAsync(orderId, staffId, order.Total);
        return (true, null, orderId, customer.Id);
    }

    public async Task<long?> GetCustomerIdForOrderAsync(long orderId) => (await _orderRepository.GetByIdAsync(orderId))?.CustomerId;

    public Task<IEnumerable<StaffOrderListResponse>> GetOrdersForStaffAsync(string? channel, string? status) =>
        _orderRepository.GetAllForStaffAsync(channel, status);

    private async Task<User> GetOrCreateCustomerAsync(string customerName, string customerPhone)
    {
        if (_userRepository is null)
            throw new InvalidOperationException("Pembuatan customer membutuhkan IUserRepository.");
        var customer = await _userRepository.GetByEmailOrPhoneAsync(customerPhone);
        if (customer is not null) return customer;
        customer = new User { Role = "customer", FullName = customerName, Phone = customerPhone, IsActive = true };
        customer.PasswordHash = _passwordHasher.HashPassword(customer, Guid.NewGuid().ToString("N"));
        customer.Id = await _userRepository.CreateAsync(customer);
        return customer;
    }

    private static string GenerateOrderCode() => $"MKC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..7].ToUpperInvariant()}";
}
