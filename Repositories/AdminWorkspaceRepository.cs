using System.Data;
using Dapper;
using MyKicksBuddy.Models.Dtos;

namespace MyKicksBuddy.Repositories;

public interface IAdminWorkspaceRepository
{
    Task<(IReadOnlyList<PortalOrderRow> Rows, int Count)> SearchOrdersAsync(string? search, string? status, string? paymentStatus, DateTime? from, DateTime? to, int page, int pageSize);
    Task<AdminReportViewModel> GetReportAsync(DateTime from, DateTime to);
    Task<IReadOnlyList<AdminPaymentRow>> GetPaymentsAsync(string? search, string? status);
    Task<IReadOnlyList<AdminEventRow>> GetEventsAsync(int limit);
    Task<bool> CheckDatabaseAsync();
}

public sealed class AdminWorkspaceRepository : IAdminWorkspaceRepository
{
    private readonly IDbConnection _db;
    public AdminWorkspaceRepository(IDbConnection db) => _db = db;

    public async Task<(IReadOnlyList<PortalOrderRow> Rows, int Count)> SearchOrdersAsync(string? search, string? status, string? paymentStatus, DateTime? from, DateTime? to, int page, int pageSize)
    {
        const string where = @" FROM orders o JOIN users u ON u.id=o.customer_id
            WHERE (@Status IS NULL OR o.status=@Status)
              AND (@PaymentStatus IS NULL OR o.payment_status=@PaymentStatus)
              AND (@From IS NULL OR o.created_at>=@From)
              AND (@ToExclusive IS NULL OR o.created_at<@ToExclusive)
              AND (@Search IS NULL OR o.order_code LIKE @Search OR u.full_name LIKE @Search
                   OR EXISTS (SELECT 1 FROM order_items oi JOIN services s ON s.id=oi.service_id
                              WHERE oi.order_id=o.id AND s.name LIKE @Search))";
        var args = new { Search = string.IsNullOrWhiteSpace(search) ? null : "%" + search.Trim() + "%", Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim(), PaymentStatus = string.IsNullOrWhiteSpace(paymentStatus) ? null : paymentStatus.Trim(), From = from?.Date, ToExclusive = to?.Date.AddDays(1), Offset = (page - 1) * pageSize, PageSize = pageSize };
        var count = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*)" + where, args);
        var rows = await _db.QueryAsync<PortalOrderRow>(@"SELECT o.id AS Id,o.order_code AS OrderCode,u.full_name AS CustomerName,
            o.total AS TotalAmount,o.fulfillment_type AS FulfillmentType,o.status AS Status,
            o.payment_status AS PaymentStatus,o.created_at AS CreatedAt" + where + " ORDER BY o.created_at DESC,o.id DESC LIMIT @PageSize OFFSET @Offset", args);
        return (rows.ToList(), count);
    }

    public async Task<AdminReportViewModel> GetReportAsync(DateTime from, DateTime to)
    {
        var summary = await _db.QuerySingleAsync<ReportSummary>(@"SELECT COUNT(*) TotalOrders,
            COALESCE(SUM(status='completed'),0) CompletedOrders,
            COALESCE(SUM(status='in_process'),0) InProcessOrders,
            COALESCE(SUM(payment_status='paid'),0) PaidOrders,
            COALESCE(SUM(payment_status='unpaid'),0) UnpaidOrders,
            COALESCE(SUM(payment_status='failed'),0) FailedOrders,
            COALESCE(SUM(payment_status='expired'),0) ExpiredOrders,
            COALESCE(SUM(CASE WHEN payment_status='paid' THEN total ELSE 0 END),0) PaidRevenue
            FROM orders WHERE created_at>=@From AND created_at<@ToExclusive", new { From = from.Date, ToExclusive = to.Date.AddDays(1) });
        var daily = (await _db.QueryAsync<AdminDailyPoint>(@"SELECT DATE(created_at) Day,COUNT(*) Orders FROM orders
            WHERE created_at>=@From AND created_at<@ToExclusive GROUP BY DATE(created_at) ORDER BY Day", new { From = from.Date, ToExclusive = to.Date.AddDays(1) })).ToList();
        return new AdminReportViewModel { From=from.Date,To=to.Date,TotalOrders=summary.TotalOrders,CompletedOrders=summary.CompletedOrders,InProcessOrders=summary.InProcessOrders,PaidOrders=summary.PaidOrders,UnpaidOrders=summary.UnpaidOrders,FailedOrders=summary.FailedOrders,ExpiredOrders=summary.ExpiredOrders,PaidRevenue=summary.PaidRevenue,DailyOrders=daily };
    }

    public async Task<IReadOnlyList<AdminPaymentRow>> GetPaymentsAsync(string? search, string? status)
    {
        var rows = await _db.QueryAsync<AdminPaymentRow>(@"SELECT o.id OrderId,o.order_code OrderCode,u.full_name CustomerName,
            o.total TotalAmount,o.payment_status PaymentStatus,p.provider Provider,p.provider_reference ProviderReference,
            o.created_at CreatedAt,p.verified_at VerifiedAt,(p.id IS NOT NULL) HasAttempt
            FROM orders o JOIN users u ON u.id=o.customer_id LEFT JOIN payments p ON p.order_id=o.id
            WHERE (@Status IS NULL OR o.payment_status=@Status)
              AND (@Search IS NULL OR o.order_code LIKE @Search OR u.full_name LIKE @Search)
            ORDER BY o.created_at DESC,o.id DESC LIMIT 500", new { Search = string.IsNullOrWhiteSpace(search) ? null : "%" + search.Trim() + "%", Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim() });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<AdminEventRow>> GetEventsAsync(int limit)
    {
        var rows = await _db.QueryAsync<AdminEventRow>(@"SELECT l.order_id OrderId,o.order_code OrderCode,l.status Status,l.note Note,l.created_at CreatedAt
            FROM order_status_log l JOIN orders o ON o.id=l.order_id ORDER BY l.created_at DESC,l.id DESC LIMIT @Limit", new { Limit = limit });
        return rows.ToList();
    }

    public async Task<bool> CheckDatabaseAsync()
    {
        try { return await _db.ExecuteScalarAsync<int>("SELECT 1") == 1; }
        catch (Exception ex) when (ex is System.Data.Common.DbException or InvalidOperationException) { return false; }
    }

    private sealed class ReportSummary
    {
        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int InProcessOrders { get; set; }
        public int PaidOrders { get; set; }
        public int UnpaidOrders { get; set; }
        public int FailedOrders { get; set; }
        public int ExpiredOrders { get; set; }
        public decimal PaidRevenue { get; set; }
    }
}
