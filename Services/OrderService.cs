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

        string orderCode = $"MKC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";

        var order = new Order
        {
            OrderCode = orderCode,
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

        try
        {
            var orderId = await _orderRepository.CreateWithItemsAsync(order, request.Items);
            return new OrderCreationResult(true, null, orderId, orderCode);
        }
        catch (InvalidOperationException ex)
        {
            return OrderCreationResult.Failed(ex.Message);
        }
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
}
