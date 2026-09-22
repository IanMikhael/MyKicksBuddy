using System.Data;
using Dapper;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Models.Dtos;

namespace MyKicksBuddy.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IDbConnection _db;

    public OrderRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<long> CreateAsync(Order order)
    {
        const string sql = @"
            INSERT INTO orders 
                (order_code, customer_id, channel, fulfillment_type, address_id, 
                 distance_km, subtotal, total, status, payment_status, notes)
            VALUES 
                (@OrderCode, @CustomerId, @Channel, @FulfillmentType, @AddressId, 
                 @DistanceKm, @Subtotal, @Total, @Status, @PaymentStatus, @Notes);
            SELECT LAST_INSERT_ID();";

        return await _db.ExecuteScalarAsync<long>(sql, order);
    }

    public async Task<Order?> GetByIdAsync(long id)
    {
        const string sql = @"
            SELECT id AS Id, order_code AS OrderCode, customer_id AS CustomerId,
                   channel AS Channel, fulfillment_type AS FulfillmentType,
                   address_id AS AddressId, distance_km AS DistanceKm,
                   subtotal AS Subtotal, total AS Total, status AS Status,
                   payment_status AS PaymentStatus, handled_by AS HandledBy,
                   notes AS Notes, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM orders
            WHERE id = @Id";

        return await _db.QueryFirstOrDefaultAsync<Order>(sql, new { Id = id });
    }

    public async Task<bool> UpdateStatusWithLogAsync(long orderId, string expectedStatus, string status, long handledBy, string? notes)
    {
        if (_db.State != ConnectionState.Open) _db.Open();
        using var transaction = _db.BeginTransaction();
        try
        {
            const string updateOrderSql = @"
                UPDATE orders 
                SET status = @Status, handled_by = @HandledBy, updated_at = CURRENT_TIMESTAMP
                WHERE id = @OrderId AND status = @ExpectedStatus
                  AND ((@Status = 'cancelled' AND payment_status <> 'paid')
                       OR (@Status <> 'cancelled' AND payment_status = 'paid'))
                  AND ((@ExpectedStatus = 'pending_payment' AND @Status = 'cancelled')
                       OR (@ExpectedStatus = 'waiting_approval' AND @Status IN ('approved','cancelled'))
                       OR (@ExpectedStatus = 'approved' AND @Status = 'waiting_pickup')
                       OR (@ExpectedStatus = 'waiting_pickup' AND @Status = 'picked_up')
                       OR (@ExpectedStatus = 'picked_up' AND @Status = 'in_process')
                       OR (@ExpectedStatus = 'in_process' AND @Status = 'ready_to_return')
                       OR (@ExpectedStatus = 'ready_to_return' AND @Status = 'returned')
                       OR (@ExpectedStatus = 'returned' AND @Status = 'completed'));";

            var changed = await _db.ExecuteAsync(updateOrderSql, new { Status = status, ExpectedStatus = expectedStatus, HandledBy = handledBy, OrderId = orderId }, transaction);
            if (changed != 1)
            {
                transaction.Rollback();
                return false;
            }

            const string insertLogSql = @"
                INSERT INTO order_status_log (order_id, status, note, changed_by, created_at)
                VALUES (@OrderId, @Status, @Notes, @HandledBy, CURRENT_TIMESTAMP);";

            await _db.ExecuteAsync(insertLogSql, new { OrderId = orderId, Status = status, Notes = notes, HandledBy = handledBy }, transaction);

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<OrderStatusLogResponse>> GetLogsByOrderIdAsync(long orderId)
    {
        const string sql = @"
            SELECT id, order_id AS OrderId, status, note, changed_by AS ChangedBy, created_at AS CreatedAt 
            FROM order_status_log 
            WHERE order_id = @OrderId 
            ORDER BY created_at ASC;";

        return await _db.QueryAsync<OrderStatusLogResponse>(sql, new { OrderId = orderId });
    }

    public async Task<IEnumerable<OrderResponse>> GetByCustomerIdAsync(long customerId)
    {
        const string sql = @"
            SELECT o.id AS Id, o.order_code AS OrderCode, o.customer_id AS CustomerId,
                (SELECT s.name FROM order_items oi
                 JOIN services s ON s.id = oi.service_id
                 WHERE oi.order_id = o.id ORDER BY oi.id LIMIT 1) AS ServiceName,
                o.total AS TotalAmount, o.fulfillment_type AS FulfillmentType,
                o.status AS Status, o.payment_status AS PaymentStatus, o.created_at AS CreatedAt
            FROM orders o
            WHERE o.customer_id = @CustomerId
            ORDER BY o.created_at DESC;";

        return await _db.QueryAsync<OrderResponse>(sql, new { CustomerId = customerId });
    }

    public async Task<OrderDetailResponse?> GetDetailByIdAndCustomerAsync(long orderId, long customerId)
    {
        const string sql = @"
            SELECT 
                o.id AS Id, o.order_code AS OrderCode, o.customer_id AS CustomerId,
                o.channel AS Channel, o.fulfillment_type AS FulfillmentType,
                o.subtotal AS Subtotal, o.total AS TotalAmount, o.status AS Status,
                o.payment_status AS PaymentStatus, o.notes AS Notes, o.distance_km AS DistanceKm,
                u.full_name AS CustomerName, u.email AS CustomerEmail, u.phone AS CustomerPhone,
                a.label AS AddressLabel, a.full_address AS FullAddress, o.created_at AS CreatedAt,
                oi.id AS ItemId, oi.service_id AS ServiceId, s.name AS ServiceName,
                oi.qty AS Quantity, oi.price AS Price, oi.subtotal AS Subtotal,
                oi.shoe_description AS ShoeDescription
            FROM orders o
            INNER JOIN users u ON u.id = o.customer_id
            LEFT JOIN customer_addresses a ON a.id = o.address_id
            LEFT JOIN order_items oi ON o.id = oi.order_id
            LEFT JOIN services s ON oi.service_id = s.id
            WHERE o.id = @OrderId AND o.customer_id = @CustomerId;";

        OrderDetailResponse? orderResponse = null;

        await _db.QueryAsync<OrderDetailResponse, OrderItemDto, OrderDetailResponse>(
            sql,
            (order, item) =>
            {
                if (orderResponse == null)
                {
                    orderResponse = order;
                    orderResponse.Items = new List<OrderItemDto>();
                }
                if (item != null && item.ItemId > 0)
                {
                    orderResponse.Items.Add(item);
                }
                return orderResponse;
            },
            new { OrderId = orderId, CustomerId = customerId },
            splitOn: "ItemId"
        );

        return orderResponse;
    }

   public async Task<OrderDetailResponse?> GetDetailByIdAsync(long orderId)
    {
        const string sql = @"
            SELECT 
                o.id AS Id, o.order_code AS OrderCode, o.customer_id AS CustomerId,
                o.channel AS Channel, o.fulfillment_type AS FulfillmentType,
                o.subtotal AS Subtotal, o.total AS TotalAmount, o.status AS Status,
                o.payment_status AS PaymentStatus, o.notes AS Notes, o.distance_km AS DistanceKm,
                u.full_name AS CustomerName, u.email AS CustomerEmail, u.phone AS CustomerPhone,
                a.label AS AddressLabel, a.full_address AS FullAddress, o.created_at AS CreatedAt,
                oi.id AS ItemId, oi.service_id AS ServiceId, s.name AS ServiceName,
                oi.qty AS Quantity, oi.price AS Price, oi.subtotal AS Subtotal,
                oi.shoe_description AS ShoeDescription
            FROM orders o
            INNER JOIN users u ON u.id = o.customer_id
            LEFT JOIN customer_addresses a ON a.id = o.address_id
            LEFT JOIN order_items oi ON o.id = oi.order_id
            LEFT JOIN services s ON oi.service_id = s.id
            WHERE o.id = @OrderId;";

        OrderDetailResponse? orderResponse = null;

        await _db.QueryAsync<OrderDetailResponse, OrderItemDto, OrderDetailResponse>(
            sql,
            (order, item) =>
            {
                if (orderResponse == null)
                {
                    orderResponse = order;
                    orderResponse.Items = new List<OrderItemDto>();
                }
                if (item != null && item.ItemId > 0)
                {
                    orderResponse.Items.Add(item);
                }
                return orderResponse;
            },
            new { OrderId = orderId },
            splitOn: "ItemId"
        );

        return orderResponse;
    }

    public async Task<long> CreateWithItemsAsync(Order order, IReadOnlyCollection<OrderItemRequest> items)
    {
        if (_db.State != ConnectionState.Open) _db.Open();
        using var transaction = _db.BeginTransaction();
        try
        {
            var serviceIds = items.Select(item => item.ServiceId).Distinct().ToArray();
            var services = (await _db.QueryAsync<ServiceOptionDto>(@"
                SELECT id AS Id, name AS Name, description AS Description,
                       price AS Price, estimated_duration_days AS EstimatedDurationDays
                FROM services
                WHERE is_active = 1 AND id IN @Ids",
                new { Ids = serviceIds }, transaction)).ToDictionary(service => service.Id);

            if (services.Count != serviceIds.Length)
                throw new InvalidOperationException("Satu atau lebih layanan tidak tersedia.");

            var calculatedSubtotal = items.Sum(item => services[item.ServiceId].Price * item.Quantity);

            order.Subtotal = calculatedSubtotal;
            order.Total = calculatedSubtotal; // Tambahkan ongkir di sini jika nanti diperlukan

            // 2. Insert ke tabel orders
            const string insertOrderSql = @"
                INSERT INTO orders 
                    (order_code, customer_id, channel, fulfillment_type, address_id, 
                    distance_km, subtotal, total, status, payment_status, handled_by, notes)
                VALUES 
                    (@OrderCode, @CustomerId, @Channel, @FulfillmentType, @AddressId, 
                    @DistanceKm, @Subtotal, @Total, @Status, @PaymentStatus, @HandledBy, @Notes);
                SELECT LAST_INSERT_ID();";

            long orderId = await _db.ExecuteScalarAsync<long>(insertOrderSql, order, transaction);

            await _db.ExecuteAsync(@"
                INSERT INTO order_status_log (order_id, status, note, changed_by, created_at)
                VALUES (@OrderId, @Status, NULL, NULL, CURRENT_TIMESTAMP)",
                new { OrderId = orderId, order.Status }, transaction);

            foreach (var item in items)
            {
                var servicePrice = services[item.ServiceId].Price;
                decimal itemSubtotal = servicePrice * item.Quantity;

                const string insertItemSql = @"
                    INSERT INTO order_items (order_id, service_id, shoe_description, qty, price, subtotal)
                    VALUES (@OrderId, @ServiceId, @ShoeDescription, @Qty, @Price, @Subtotal);";

                await _db.ExecuteAsync(insertItemSql, new {
                    OrderId = orderId,
                    ServiceId = item.ServiceId,
                    ShoeDescription = item.ShoeDescription,
                    Qty = item.Quantity,
                    Price = servicePrice,
                    Subtotal = itemSubtotal
                }, transaction);
            }

            transaction.Commit();
            return orderId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<OrderDetailResponse?> GetDetailByCodeAndCustomerAsync(string orderCode, long customerId)
    {
        const string sql = @"
            SELECT 
                o.id AS Id, o.order_code AS OrderCode, o.customer_id AS CustomerId,
                o.channel AS Channel, o.fulfillment_type AS FulfillmentType,
                o.subtotal AS Subtotal, o.total AS TotalAmount, o.status AS Status,
                o.payment_status AS PaymentStatus, o.notes AS Notes, o.distance_km AS DistanceKm,
                u.full_name AS CustomerName, u.email AS CustomerEmail, u.phone AS CustomerPhone,
                a.label AS AddressLabel, a.full_address AS FullAddress, o.created_at AS CreatedAt,
                oi.id AS ItemId, oi.service_id AS ServiceId, s.name AS ServiceName,
                oi.qty AS Quantity, oi.price AS Price, oi.subtotal AS Subtotal,
                oi.shoe_description AS ShoeDescription
            FROM orders o
            INNER JOIN users u ON u.id = o.customer_id
            LEFT JOIN customer_addresses a ON a.id = o.address_id
            LEFT JOIN order_items oi ON o.id = oi.order_id
            LEFT JOIN services s ON oi.service_id = s.id
            WHERE o.order_code = @OrderCode AND o.customer_id = @CustomerId;";

        OrderDetailResponse? orderResponse = null;

        await _db.QueryAsync<OrderDetailResponse, OrderItemDto, OrderDetailResponse>(
            sql,
            (order, item) =>
            {
                if (orderResponse == null)
                {
                    orderResponse = order;
                    orderResponse.Items = new List<OrderItemDto>();
                }
                if (item != null && item.ItemId > 0)
                {
                    orderResponse.Items.Add(item);
                }
                return orderResponse;
            },
            new { OrderCode = orderCode, CustomerId = customerId },
            splitOn: "ItemId"
        );

        return orderResponse;
    }

    public async Task<IReadOnlyList<ServiceOptionDto>> GetActiveServicesAsync()
    {
        const string sql = @"
            SELECT id AS Id, name AS Name, description AS Description, price AS Price,
                   estimated_duration_days AS EstimatedDurationDays
            FROM services
            WHERE is_active = 1
            ORDER BY name ASC;";

        return (await _db.QueryAsync<ServiceOptionDto>(sql)).ToList();
    }

    public async Task<IReadOnlyList<PortalOrderRow>> GetAllAsync(string? status, DateTime? from, DateTime? to, int? limit = null)
    {
        const string sql = @"
            SELECT o.id AS Id, o.order_code AS OrderCode, u.full_name AS CustomerName,
                   o.total AS TotalAmount, o.fulfillment_type AS FulfillmentType,
                   o.status AS Status, o.payment_status AS PaymentStatus, o.created_at AS CreatedAt
            FROM orders o
            INNER JOIN users u ON u.id = o.customer_id
            WHERE (@Status IS NULL OR o.status = @Status)
              AND (@From IS NULL OR o.created_at >= @From)
              AND (@ToExclusive IS NULL OR o.created_at < @ToExclusive)
            ORDER BY o.created_at DESC
            LIMIT @RowLimit;";
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        return (await _db.QueryAsync<PortalOrderRow>(sql, new
        {
            Status = normalizedStatus,
            From = from?.Date,
            ToExclusive = to?.Date.AddDays(1),
            RowLimit = limit ?? 500
        })).ToList();
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        const string sql = @"
            SELECT COUNT(*) AS TotalOrders,
                COALESCE(SUM(status = 'waiting_approval'), 0) AS WaitingApprovalOrders,
                COALESCE(SUM(payment_status = 'paid'), 0) AS PaidOrders,
                COALESCE(SUM(status NOT IN ('completed','cancelled')), 0) AS ActiveOrders,
                COALESCE(SUM(status = 'completed'), 0) AS CompletedOrders,
                COALESCE(SUM(status = 'cancelled'), 0) AS CancelledOrders,
                COALESCE(SUM(status = 'pending_payment' AND payment_status = 'unpaid'), 0) AS PendingPayments,
                COALESCE(SUM(CASE WHEN payment_status = 'paid' THEN total ELSE 0 END), 0) AS PaidRevenue
            FROM orders;";
        return await _db.QuerySingleAsync<DashboardSummary>(sql);
    }

    public async Task<OrderDetailResponse?> GetDetailByCodeAsync(string orderCode)
    {
        const string sql = @"
            SELECT o.id AS Id, o.order_code AS OrderCode, o.customer_id AS CustomerId,
                   o.channel AS Channel, o.fulfillment_type AS FulfillmentType,
                   o.subtotal AS Subtotal, o.total AS TotalAmount, o.status AS Status,
                   o.payment_status AS PaymentStatus, o.notes AS Notes, o.distance_km AS DistanceKm,
                   u.full_name AS CustomerName, u.email AS CustomerEmail, u.phone AS CustomerPhone,
                   a.label AS AddressLabel, a.full_address AS FullAddress, o.created_at AS CreatedAt,
                   oi.id AS ItemId, oi.service_id AS ServiceId, s.name AS ServiceName,
                   oi.qty AS Quantity, oi.price AS Price, oi.subtotal AS Subtotal,
                   oi.shoe_description AS ShoeDescription
            FROM orders o
            INNER JOIN users u ON u.id = o.customer_id
            LEFT JOIN customer_addresses a ON a.id = o.address_id
            LEFT JOIN order_items oi ON o.id = oi.order_id
            LEFT JOIN services s ON oi.service_id = s.id
            WHERE o.order_code = @OrderCode;";

        OrderDetailResponse? orderResponse = null;
        await _db.QueryAsync<OrderDetailResponse, OrderItemDto, OrderDetailResponse>(
            sql,
            (order, item) =>
            {
                orderResponse ??= order;
                orderResponse.Items ??= new List<OrderItemDto>();
                if (item?.ItemId > 0) orderResponse.Items.Add(item);
                return orderResponse;
            },
            new { OrderCode = orderCode },
            splitOn: "ItemId");
        return orderResponse;
    }

    public async Task<IEnumerable<ServiceDto>> GetAllServicesAsync()
    {
        const string sql = @"
            SELECT id AS Id, name AS Name, price AS Price,
                   estimated_hours AS EstimatedHours, is_active AS IsActive
            FROM services WHERE is_active = 1 ORDER BY name ASC;";
        return await _db.QueryAsync<ServiceDto>(sql);
    }

    public async Task MarkPaidCashAsync(long orderId, long staffId, decimal grossAmount)
    {
        if (_db.State != ConnectionState.Open) _db.Open();
        using var transaction = _db.BeginTransaction();
        try
        {
            const string updateOrderSql = @"
                UPDATE orders
                SET status = 'waiting_approval', payment_status = 'paid', handled_by = @HandledBy, updated_at = CURRENT_TIMESTAMP
                WHERE id = @OrderId AND status = 'pending_payment' AND payment_status = 'unpaid';";
            if (await _db.ExecuteAsync(updateOrderSql, new { OrderId = orderId, HandledBy = staffId }, transaction) != 1)
                throw new InvalidOperationException("Pesanan tidak lagi menunggu pembayaran.");

            await _db.ExecuteAsync(@"
                INSERT INTO order_status_log (order_id, status, note, changed_by, created_at)
                VALUES (@OrderId, 'waiting_approval', 'Pembayaran cash diterima di kasir.', @HandledBy, CURRENT_TIMESTAMP);",
                new { OrderId = orderId, HandledBy = staffId }, transaction);

            await _db.ExecuteAsync(@"
                INSERT INTO payments (order_id, provider_order_id, gross_amount, currency, status, payment_type, paid_at)
                VALUES (@OrderId, @ProviderOrderId, @GrossAmount, 'IDR', 'paid', 'cash', CURRENT_TIMESTAMP);",
                new { OrderId = orderId, ProviderOrderId = $"CASH-{Guid.NewGuid():N}", GrossAmount = grossAmount }, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<StaffOrderListResponse>> GetAllForStaffAsync(string? channel, string? status)
    {
        const string sql = @"
            SELECT o.id AS Id, o.order_code AS OrderCode, o.channel AS Channel,
                   u.full_name AS CustomerName, u.phone AS CustomerPhone,
                   o.fulfillment_type AS FulfillmentType, o.total AS TotalAmount,
                   o.status AS Status, o.payment_status AS PaymentStatus,
                   o.handled_by AS HandledBy, o.created_at AS CreatedAt, o.updated_at AS UpdatedAt
            FROM orders o JOIN users u ON u.id = o.customer_id
            WHERE (@Channel IS NULL OR o.channel = @Channel)
              AND (@Status IS NULL OR o.status = @Status)
            ORDER BY o.created_at DESC;";
        return await _db.QueryAsync<StaffOrderListResponse>(sql, new { Channel = channel, Status = status });
    }
}
