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

    public async Task UpdateStatusAsync(long id, string status, string paymentStatus)
    {
        const string sql = @"
            UPDATE orders 
            SET status = @Status, payment_status = @PaymentStatus
            WHERE id = @Id";

        await _db.ExecuteAsync(sql, new { Id = id, Status = status, PaymentStatus = paymentStatus });
    }

    public async Task UpdateStatusWithLogAsync(long orderId, string status, long handledBy, string? notes)
    {
        if (_db.State != ConnectionState.Open) _db.Open();
        using var transaction = _db.BeginTransaction();
        try
        {
            const string updateOrderSql = @"
                UPDATE orders 
                SET status = @Status, handled_by = @HandledBy, updated_at = CURRENT_TIMESTAMP
                WHERE id = @OrderId;";

            await _db.ExecuteAsync(updateOrderSql, new { Status = status, HandledBy = handledBy, OrderId = orderId }, transaction);

            const string insertLogSql = @"
                INSERT INTO order_status_log (order_id, status, note, created_at)
                VALUES (@OrderId, @Status, @Notes, CURRENT_TIMESTAMP);";

            await _db.ExecuteAsync(insertLogSql, new { OrderId = orderId, Status = status, Notes = notes }, transaction);

            transaction.Commit();
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
            SELECT id AS Id, customer_id AS CustomerId, total AS TotalAmount, 
                status AS Status, payment_status AS PaymentStatus, created_at AS CreatedAt 
            FROM orders 
            WHERE customer_id = @CustomerId 
            ORDER BY created_at DESC;";

        return await _db.QueryAsync<OrderResponse>(sql, new { CustomerId = customerId });
    }

    public async Task<OrderDetailResponse?> GetDetailByIdAndCustomerAsync(long orderId, long customerId)
    {
        const string sql = @"
            SELECT 
                o.id AS Id, o.order_code AS OrderCode, o.customer_id AS CustomerId,
                o.channel AS Channel, o.fulfillment_type AS FulfillmentType,
                o.subtotal AS Subtotal, o.total AS TotalAmount, o.status AS Status,
                o.payment_status AS PaymentStatus, o.notes AS Notes, o.created_at AS CreatedAt,
                oi.service_id AS ServiceId, s.name AS ServiceName,
                oi.qty AS Quantity, oi.price AS Price, oi.subtotal AS Subtotal,
                oi.id AS Id
            FROM orders o
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
                if (item != null && item.Id > 0)
                {
                    orderResponse.Items.Add(item);
                }
                return orderResponse;
            },
            new { OrderId = orderId, CustomerId = customerId },
            splitOn: "ServiceId"
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
                o.payment_status AS PaymentStatus, o.notes AS Notes, o.created_at AS CreatedAt,
                oi.service_id AS ServiceId, s.name AS ServiceName,
                oi.qty AS Quantity, oi.price AS Price, oi.subtotal AS Subtotal,
                oi.shoe_description AS ShoeDescription, -- <-- Tambahkan baris ini
                oi.id AS Id
            FROM orders o
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
                if (item != null && item.Id > 0)
                {
                    orderResponse.Items.Add(item);
                }
                return orderResponse;
            },
            new { OrderId = orderId },
            splitOn: "ServiceId"
        );

        return orderResponse;
    }

    public async Task<long> CreateWithItemsAsync(Order order, IEnumerable<OrderItemRequest> items)
    {
        if (_db.State != ConnectionState.Open) _db.Open();
        using var transaction = _db.BeginTransaction();
        try
        {
            decimal calculatedSubtotal = 0;

            // 1. Hitung total harga berdasarkan harga asli dari tabel services
            foreach (var item in items)
            {
                var servicePrice = await _db.QueryFirstOrDefaultAsync<decimal>(
                    "SELECT price FROM services WHERE id = @ServiceId", 
                    new { item.ServiceId }, 
                    transaction);

                calculatedSubtotal += servicePrice * item.Quantity;
            }

            order.Subtotal = calculatedSubtotal;
            order.Total = calculatedSubtotal; // Tambahkan ongkir di sini jika nanti diperlukan

            // 2. Insert ke tabel orders
            const string insertOrderSql = @"
                INSERT INTO orders 
                    (order_code, customer_id, channel, fulfillment_type, address_id, 
                    distance_km, subtotal, total, status, payment_status, notes)
                VALUES 
                    (@OrderCode, @CustomerId, @Channel, @FulfillmentType, @AddressId, 
                    @DistanceKm, @Subtotal, @Total, @Status, @PaymentStatus, @Notes);
                SELECT LAST_INSERT_ID();";

            long orderId = await _db.ExecuteScalarAsync<long>(insertOrderSql, order, transaction);

            // 3. Insert setiap item ke tabel order_items
            foreach (var item in items)
            {
                var servicePrice = await _db.QueryFirstOrDefaultAsync<decimal>(
                    "SELECT price FROM services WHERE id = @ServiceId", 
                    new { item.ServiceId }, 
                    transaction);

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

    public async Task<OrderDetailResponse?> GetDetailByCodeAsync(string orderCode)
    {
        const string sql = @"
            SELECT 
                o.id AS Id, o.order_code AS OrderCode, o.customer_id AS CustomerId,
                o.channel AS Channel, o.fulfillment_type AS FulfillmentType,
                o.subtotal AS Subtotal, o.total AS TotalAmount, o.status AS Status,
                o.payment_status AS PaymentStatus, o.notes AS Notes, o.created_at AS CreatedAt,
                oi.service_id AS ServiceId, s.name AS ServiceName,
                oi.qty AS Quantity, oi.price AS Price, oi.subtotal AS Subtotal,
                oi.shoe_description AS ShoeDescription,
                oi.id AS Id
            FROM orders o
            LEFT JOIN order_items oi ON o.id = oi.order_id
            LEFT JOIN services s ON oi.service_id = s.id
            WHERE o.order_code = @OrderCode;";

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
                if (item != null && item.Id > 0)
                {
                    orderResponse.Items.Add(item);
                }
                return orderResponse;
            },
            new { OrderCode = orderCode },
            splitOn: "ServiceId"
        );

        return orderResponse;
    }

    public async Task<IEnumerable<object>> GetAllServicesAsync()
    {
        const string sql = @"
            SELECT id AS Id, name AS Name, price AS Price
            FROM services
            ORDER BY name ASC;";

        return await _db.QueryAsync<object>(sql);
    }
}