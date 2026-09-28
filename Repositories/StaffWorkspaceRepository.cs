using System.Data;
using Dapper;
using MyKicksBuddy.Models.Dtos;

namespace MyKicksBuddy.Repositories;

public sealed class StaffWorkspaceRepository : IStaffWorkspaceRepository
{
    private readonly IDbConnection _db;
    public StaffWorkspaceRepository(IDbConnection db) => _db = db;

    public Task<StaffDashboardCounts> GetCountsAsync() => _db.QuerySingleAsync<StaffDashboardCounts>(@"
        SELECT COALESCE(SUM(status IN ('waiting_approval','approved')),0) AS Incoming,
               COALESCE(SUM(status IN ('waiting_approval','approved','ready_to_return') AND payment_status='paid'),0) AS NeedsAction,
               COALESCE(SUM(status='waiting_pickup' AND fulfillment_type='pickup_delivery'),0) AS Pickup,
               COALESCE(SUM(payment_status='paid'),0) AS Paid
        FROM orders");

    public async Task<(IReadOnlyList<StaffOrderRow> Rows, int Total)> GetOrdersAsync(
        string? query, string? status, string? paymentStatus, bool pickupOnly, int page, int pageSize)
    {
        const string where = @"
            FROM orders o JOIN users u ON u.id=o.customer_id
            LEFT JOIN customer_addresses a ON a.id=o.address_id
            WHERE (@Query IS NULL OR o.order_code LIKE @Pattern OR u.full_name LIKE @Pattern
              OR EXISTS (SELECT 1 FROM order_items oi JOIN services s ON s.id=oi.service_id
                         WHERE oi.order_id=o.id AND s.name LIKE @Pattern))
              AND (@Status IS NULL OR o.status=@Status)
              AND (@PaymentStatus IS NULL OR o.payment_status=@PaymentStatus)
              AND (@PickupOnly=0 OR (o.fulfillment_type='pickup_delivery' AND o.status IN ('approved','waiting_pickup','picked_up')))";
        var args = new
        {
            Query = string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
            Pattern = $"%{query?.Trim()}%",
            Status = status,
            PaymentStatus = paymentStatus,
            PickupOnly = pickupOnly ? 1 : 0,
            Limit = pageSize,
            Offset = (page - 1) * pageSize
        };
        var total = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) " + where, args);
        var rows = await _db.QueryAsync<StaffOrderRow>(@"
            SELECT o.id AS Id, o.order_code AS OrderCode, u.full_name AS CustomerName,
                   (SELECT s.name FROM order_items oi JOIN services s ON s.id=oi.service_id
                    WHERE oi.order_id=o.id ORDER BY oi.id LIMIT 1) AS ServiceName,
                   o.status AS Status, o.payment_status AS PaymentStatus,
                   o.fulfillment_type AS FulfillmentType, a.full_address AS FullAddress,
                   o.total AS TotalAmount, o.created_at AS CreatedAt " + where + @"
            ORDER BY o.created_at DESC, o.id DESC LIMIT @Limit OFFSET @Offset", args);
        return (rows.ToList(), total);
    }

    public async Task<(IReadOnlyList<StaffPaymentRow> Rows, int Total)> GetPaymentsAsync(string? query, string? status, int page, int pageSize)
    {
        const string where = @"
            FROM orders o JOIN users u ON u.id=o.customer_id
            LEFT JOIN payments p ON p.order_id=o.id
            WHERE (@Query IS NULL OR o.order_code LIKE @Pattern OR u.full_name LIKE @Pattern)
              AND (@Status IS NULL OR o.payment_status=@Status)";
        var args = new { Query = string.IsNullOrWhiteSpace(query) ? null : query.Trim(), Pattern = $"%{query?.Trim()}%", Status = status, Limit = pageSize, Offset = (page - 1) * pageSize };
        var total = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) " + where, args);
        var rows = await _db.QueryAsync<StaffPaymentRow>(@"
            SELECT o.id AS OrderId, o.order_code AS OrderCode, u.full_name AS CustomerName,
                   (SELECT s.name FROM order_items oi JOIN services s ON s.id=oi.service_id
                    WHERE oi.order_id=o.id ORDER BY oi.id LIMIT 1) AS ServiceName,
                   o.total AS TotalAmount, o.status AS OrderStatus,
                   o.payment_status AS PaymentStatus, p.provider AS Provider,
                   p.provider_reference AS ProviderReference, o.created_at AS CreatedAt,
                   p.verified_at AS VerifiedAt " + where + @"
            ORDER BY o.created_at DESC, o.id DESC LIMIT @Limit OFFSET @Offset", args);
        return (rows.ToList(), total);
    }

    public Task<StaffPaymentRow?> GetPaymentAsync(long orderId) => _db.QuerySingleOrDefaultAsync<StaffPaymentRow>(@"
        SELECT o.id AS OrderId, o.order_code AS OrderCode, u.full_name AS CustomerName,
               (SELECT s.name FROM order_items oi JOIN services s ON s.id=oi.service_id
                WHERE oi.order_id=o.id ORDER BY oi.id LIMIT 1) AS ServiceName,
               o.total AS TotalAmount, o.status AS OrderStatus,
               o.payment_status AS PaymentStatus, p.provider AS Provider,
               p.provider_reference AS ProviderReference, o.created_at AS CreatedAt,
               p.verified_at AS VerifiedAt
        FROM orders o JOIN users u ON u.id=o.customer_id
        LEFT JOIN payments p ON p.order_id=o.id WHERE o.id=@OrderId", new { OrderId = orderId });

    public async Task<IReadOnlyList<StaffActivityRow>> GetActivityAsync(int limit) =>
        (await _db.QueryAsync<StaffActivityRow>(@"
            SELECT l.order_id AS OrderId, o.order_code AS OrderCode,
                   u.full_name AS CustomerName, l.status AS Status,
                   l.note AS Note, l.created_at AS CreatedAt
            FROM order_status_log l JOIN orders o ON o.id=l.order_id
            JOIN users u ON u.id=o.customer_id
            ORDER BY l.created_at DESC, l.id DESC LIMIT @Limit", new { Limit = limit })).ToList();
}
