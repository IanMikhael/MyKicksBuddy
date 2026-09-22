using System.Data;
using Dapper;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private sealed class OrderPaymentState
    {
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
    }

    private readonly IDbConnection _db;
    public PaymentRepository(IDbConnection db) => _db = db;

    public Task<Payment?> GetByOrderIdAsync(long orderId) => _db.QuerySingleOrDefaultAsync<Payment>(@"
        SELECT id AS Id, order_id AS OrderId, provider AS Provider,
               provider_reference AS ProviderReference, amount AS Amount,
               currency AS Currency, status AS Status, verified_at AS VerifiedAt
        FROM payments WHERE order_id = @OrderId", new { OrderId = orderId });

    public async Task<Payment> CreateAsync(long orderId, PaymentCreation creation, decimal expectedAmount)
    {
        if (_db.State != ConnectionState.Open) _db.Open();
        using var transaction = _db.BeginTransaction();
        try
        {
            var amount = await _db.QuerySingleOrDefaultAsync<decimal?>(@"
                SELECT total FROM orders
                WHERE id = @OrderId AND status = 'pending_payment' AND payment_status = 'unpaid'
                FOR UPDATE", new { OrderId = orderId }, transaction);
            if (amount is null || amount != expectedAmount || amount <= 0)
                throw new InvalidOperationException("Pesanan atau total pembayaran telah berubah.");

            await _db.ExecuteAsync(@"
                INSERT INTO payments (order_id, provider, provider_reference, amount, currency, status)
                VALUES (@OrderId, @Provider, @ProviderReference, @Amount, 'IDR', 'unpaid')",
                new { OrderId = orderId, creation.Provider, creation.ProviderReference, Amount = amount.Value }, transaction);
            transaction.Commit();
            return (await GetByOrderIdAsync(orderId))!;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> ApplyVerifiedResultAsync(VerifiedPaymentResult result)
    {
        if (result.Status is not (PaymentStatusWorkflow.Paid or PaymentStatusWorkflow.Failed or PaymentStatusWorkflow.Expired))
            throw new InvalidOperationException("Hasil pembayaran tidak dikenal.");
        if (string.IsNullOrWhiteSpace(result.Provider) || string.IsNullOrWhiteSpace(result.ProviderReference) ||
            result.Amount <= 0 || result.Currency != "IDR")
            throw new InvalidOperationException("Identitas atau nilai pembayaran tidak valid.");
        if (_db.State != ConnectionState.Open) _db.Open();
        using var transaction = _db.BeginTransaction();
        try
        {
            var payment = await _db.QuerySingleOrDefaultAsync<Payment>(@"
                SELECT id AS Id, order_id AS OrderId, provider AS Provider,
                       provider_reference AS ProviderReference, amount AS Amount,
                       currency AS Currency, status AS Status, verified_at AS VerifiedAt
                FROM payments
                WHERE provider = @Provider AND provider_reference = @ProviderReference
                FOR UPDATE", result, transaction);
            if (payment is null || payment.Amount != result.Amount || payment.Currency != result.Currency)
                throw new InvalidOperationException("Referensi atau jumlah pembayaran tidak cocok.");
            if (payment.Status == result.Status)
            {
                transaction.Commit();
                return false; // repeated verified notification: no duplicate status log
            }
            if (payment.Status != PaymentStatusWorkflow.Unpaid)
                throw new InvalidOperationException("Hasil pembayaran bertentangan dengan status final sebelumnya.");

            var order = await _db.QuerySingleOrDefaultAsync<OrderPaymentState>(@"
                SELECT status AS Status, payment_status AS PaymentStatus FROM orders
                WHERE id = @OrderId FOR UPDATE", new { payment.OrderId }, transaction);
            if (order is null || order.Status != OrderStatusWorkflow.PendingPayment || order.PaymentStatus != PaymentStatusWorkflow.Unpaid)
                throw new InvalidOperationException("Pesanan tidak lagi menunggu pembayaran.");

            await _db.ExecuteAsync(@"
                UPDATE payments SET status = @Status, verified_at = CURRENT_TIMESTAMP,
                    updated_at = CURRENT_TIMESTAMP WHERE id = @Id",
                new { payment.Id, result.Status }, transaction);
            var orderStatus = result.Status == PaymentStatusWorkflow.Paid
                ? OrderStatusWorkflow.WaitingApproval : OrderStatusWorkflow.PendingPayment;
            await _db.ExecuteAsync(@"
                UPDATE orders SET payment_status = @PaymentStatus, status = @OrderStatus,
                    updated_at = CURRENT_TIMESTAMP WHERE id = @OrderId",
                new { PaymentStatus = result.Status, OrderStatus = orderStatus, payment.OrderId }, transaction);
            if (result.Status == PaymentStatusWorkflow.Paid)
                await _db.ExecuteAsync(@"
                    INSERT INTO order_status_log (order_id, status, note, changed_by, created_at)
                    VALUES (@OrderId, @Status, 'Pembayaran terverifikasi oleh penyedia.', NULL, CURRENT_TIMESTAMP)",
                    new { payment.OrderId, Status = orderStatus }, transaction);
            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
