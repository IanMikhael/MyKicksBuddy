using System.Data;
using Dapper;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly IDbConnection _db;

    public PaymentRepository(IDbConnection db)
    {
        _db = db;
    }

    public async Task<(Payment Payment, bool Created)> GetOrCreateActiveAsync(long orderId, string providerOrderId, decimal grossAmount)
    {
        var openedHere = _db.State != ConnectionState.Open;
        if (openedHere)
            _db.Open();

        using var transaction = _db.BeginTransaction();
        try
        {
            var lockedOrder = await _db.QueryFirstOrDefaultAsync<LockedOrder>(@"
                SELECT id AS Id, status AS Status, payment_status AS PaymentStatus, total AS Total
                FROM orders WHERE id = @OrderId FOR UPDATE",
                new { OrderId = orderId },
                transaction);

            if (lockedOrder is null ||
                !lockedOrder.Status.Equals("pending_payment", StringComparison.OrdinalIgnoreCase) ||
                lockedOrder.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase))
                throw new PaymentOrderUnavailableException("Pesanan tidak lagi menunggu pembayaran.");

            if (lockedOrder.Total != grossAmount)
                throw new PaymentOrderUnavailableException("Total pesanan berubah. Muat ulang pesanan sebelum membayar.");

            await _db.ExecuteAsync(@"
                UPDATE payments SET status = 'initiation_failed', updated_at = CURRENT_TIMESTAMP
                WHERE order_id = @OrderId AND status = 'creating'
                      AND created_at < DATE_SUB(CURRENT_TIMESTAMP, INTERVAL 2 MINUTE)",
                new { OrderId = orderId },
                transaction);

            const string activePaymentSql = @"
                SELECT id AS Id, order_id AS OrderId, provider_order_id AS ProviderOrderId,
                       transaction_id AS TransactionId, gross_amount AS GrossAmount,
                       currency AS Currency, status AS Status, snap_token AS SnapToken,
                       redirect_url AS RedirectUrl, payment_type AS PaymentType,
                       fraud_status AS FraudStatus, expires_at AS ExpiresAt,
                       (expires_at IS NOT NULL AND expires_at <= CURRENT_TIMESTAMP) AS IsExpired,
                       paid_at AS PaidAt, created_at AS CreatedAt
                FROM payments
                WHERE order_id = @OrderId AND status IN ('creating', 'pending', 'challenge')
                ORDER BY id DESC
                LIMIT 1
                FOR UPDATE";

            var activePayment = await _db.QueryFirstOrDefaultAsync<Payment>(
                activePaymentSql,
                new { OrderId = orderId },
                transaction);

            if (activePayment is not null)
            {
                transaction.Commit();
                return (activePayment, false);
            }

            const string insertSql = @"
                INSERT INTO payments (order_id, provider_order_id, gross_amount, currency, status)
                VALUES (@OrderId, @ProviderOrderId, @GrossAmount, 'IDR', 'creating');
                SELECT LAST_INSERT_ID();";

            var paymentId = await _db.ExecuteScalarAsync<long>(insertSql, new
            {
                OrderId = orderId,
                ProviderOrderId = providerOrderId,
                GrossAmount = grossAmount
            }, transaction);

            transaction.Commit();
            return (new Payment
            {
                Id = paymentId,
                OrderId = orderId,
                ProviderOrderId = providerOrderId,
                GrossAmount = grossAmount,
                Currency = "IDR",
                Status = "creating",
                CreatedAt = DateTime.UtcNow
            }, true);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            if (openedHere)
                _db.Close();
        }
    }

    public async Task<Payment?> GetLatestByOrderIdAsync(long orderId)
    {
        const string sql = @"
            SELECT id AS Id, order_id AS OrderId, provider_order_id AS ProviderOrderId,
                   transaction_id AS TransactionId, gross_amount AS GrossAmount,
                   currency AS Currency, status AS Status, snap_token AS SnapToken,
                   redirect_url AS RedirectUrl, payment_type AS PaymentType,
                   fraud_status AS FraudStatus, expires_at AS ExpiresAt,
                   (expires_at IS NOT NULL AND expires_at <= CURRENT_TIMESTAMP) AS IsExpired,
                   paid_at AS PaidAt, created_at AS CreatedAt
            FROM payments
            WHERE order_id = @OrderId
            ORDER BY id DESC
            LIMIT 1";

        return await _db.QueryFirstOrDefaultAsync<Payment>(sql, new { OrderId = orderId });
    }

    public async Task SaveSnapSessionAsync(long paymentId, string token, string redirectUrl, int expiryMinutes)
    {
        const string sql = @"
            UPDATE payments
            SET snap_token = @Token, redirect_url = @RedirectUrl,
                status = 'pending', expires_at = DATE_ADD(CURRENT_TIMESTAMP, INTERVAL @ExpiryMinutes MINUTE),
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @PaymentId AND status = 'creating'";

        var affected = await _db.ExecuteAsync(sql, new { PaymentId = paymentId, Token = token, RedirectUrl = redirectUrl, ExpiryMinutes = expiryMinutes });
        if (affected != 1)
            throw new InvalidOperationException("Percobaan pembayaran berubah sebelum sesi Snap tersimpan.");
    }

    public async Task MarkInitiationFailedAsync(long paymentId)
    {
        const string sql = @"
            UPDATE payments
            SET status = 'initiation_failed', updated_at = CURRENT_TIMESTAMP
            WHERE id = @PaymentId AND status = 'creating'";

        await _db.ExecuteAsync(sql, new { PaymentId = paymentId });
    }

    public async Task<PaymentNotificationUpdateResult> ApplyNotificationAsync(
        string providerOrderId,
        decimal grossAmount,
        string status,
        string providerStatus,
        string statusCode,
        string? transactionId,
        string? paymentType,
        string? fraudStatus)
    {
        var openedHere = _db.State != ConnectionState.Open;
        if (openedHere)
            _db.Open();

        using var transaction = _db.BeginTransaction();
        try
        {
            var paymentOrderId = await _db.QueryFirstOrDefaultAsync<long?>(@"
                SELECT order_id FROM payments WHERE provider_order_id = @ProviderOrderId LIMIT 1",
                new { ProviderOrderId = providerOrderId },
                transaction);

            if (paymentOrderId is null)
            {
                transaction.Commit();
                return PaymentNotificationUpdateResult.PaymentNotFound;
            }

            var lockedOrderId = await _db.QueryFirstOrDefaultAsync<long?>(
                "SELECT id FROM orders WHERE id = @OrderId FOR UPDATE",
                new { OrderId = paymentOrderId.Value },
                transaction);

            if (lockedOrderId is null)
            {
                transaction.Commit();
                return PaymentNotificationUpdateResult.PaymentNotFound;
            }

            const string paymentSql = @"
                SELECT id AS Id, order_id AS OrderId, provider_order_id AS ProviderOrderId,
                       transaction_id AS TransactionId, gross_amount AS GrossAmount,
                       currency AS Currency, status AS Status, snap_token AS SnapToken,
                       redirect_url AS RedirectUrl, payment_type AS PaymentType,
                       fraud_status AS FraudStatus, expires_at AS ExpiresAt,
                       paid_at AS PaidAt, created_at AS CreatedAt
                FROM payments
                WHERE provider_order_id = @ProviderOrderId
                LIMIT 1
                FOR UPDATE";

            var payment = await _db.QueryFirstOrDefaultAsync<Payment>(
                paymentSql,
                new { ProviderOrderId = providerOrderId },
                transaction);

            if (payment is null)
            {
                transaction.Commit();
                return PaymentNotificationUpdateResult.PaymentNotFound;
            }

            if (payment.GrossAmount != grossAmount)
            {
                transaction.Commit();
                return PaymentNotificationUpdateResult.AmountMismatch;
            }

            if (ShouldIgnoreTransition(payment.Status, status))
            {
                transaction.Commit();
                return PaymentNotificationUpdateResult.Ignored;
            }

            const string updatePaymentSql = @"
                UPDATE payments
                SET status = @Status,
                    transaction_id = COALESCE(@TransactionId, transaction_id),
                    payment_type = COALESCE(@PaymentType, payment_type),
                    fraud_status = COALESCE(@FraudStatus, fraud_status),
                    paid_at = CASE WHEN @Status = 'paid' THEN COALESCE(paid_at, CURRENT_TIMESTAMP) ELSE paid_at END,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @PaymentId";

            await _db.ExecuteAsync(updatePaymentSql, new
            {
                PaymentId = payment.Id,
                Status = status,
                TransactionId = transactionId,
                PaymentType = paymentType,
                FraudStatus = fraudStatus
            }, transaction);

            if (!payment.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            {
                const string insertEventSql = @"
                    INSERT INTO payment_events
                        (payment_id, previous_status, new_status, provider_status,
                         status_code, transaction_id, payment_type, fraud_status, created_at)
                    VALUES
                        (@PaymentId, @PreviousStatus, @NewStatus, @ProviderStatus,
                         @StatusCode, @TransactionId, @PaymentType, @FraudStatus, CURRENT_TIMESTAMP)";

                await _db.ExecuteAsync(insertEventSql, new
                {
                    PaymentId = payment.Id,
                    PreviousStatus = payment.Status,
                    NewStatus = status,
                    ProviderStatus = providerStatus,
                    StatusCode = statusCode,
                    TransactionId = transactionId,
                    PaymentType = paymentType,
                    FraudStatus = fraudStatus
                }, transaction);
            }

            if (status == "paid")
            {
                const string updateOrderPaymentSql = @"
                    UPDATE orders
                    SET payment_status = 'paid', updated_at = CURRENT_TIMESTAMP
                    WHERE id = @OrderId";

                await _db.ExecuteAsync(updateOrderPaymentSql, new { payment.OrderId }, transaction);

                const string advanceOrderSql = @"
                    UPDATE orders
                    SET status = 'waiting_approval', updated_at = CURRENT_TIMESTAMP
                    WHERE id = @OrderId AND status = 'pending_payment'";

                var advancedRows = await _db.ExecuteAsync(advanceOrderSql, new { payment.OrderId }, transaction);

                if (advancedRows == 1)
                {
                    const string insertLogSql = @"
                        INSERT INTO order_status_log (order_id, status, note, changed_by, created_at)
                        VALUES (@OrderId, 'waiting_approval', 'Pembayaran berhasil diterima dan pesanan menunggu persetujuan staf.', NULL, CURRENT_TIMESTAMP);";

                    await _db.ExecuteAsync(insertLogSql, new { payment.OrderId }, transaction);
                }
            }
            transaction.Commit();
            return PaymentNotificationUpdateResult.Applied;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            if (openedHere)
                _db.Close();
        }
    }

    private static bool ShouldIgnoreTransition(string currentStatus, string nextStatus)
    {
        if (string.Equals(currentStatus, nextStatus, StringComparison.OrdinalIgnoreCase))
            return false;

        if (currentStatus.Equals("refunded", StringComparison.OrdinalIgnoreCase))
            return true;

        if (currentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase) &&
            nextStatus is not ("refunded" or "partially_refunded"))
            return true;

        if (currentStatus.Equals("partially_refunded", StringComparison.OrdinalIgnoreCase) &&
            !nextStatus.Equals("refunded", StringComparison.OrdinalIgnoreCase))
            return true;

        if (currentStatus.Equals("challenge", StringComparison.OrdinalIgnoreCase) &&
            nextStatus.Equals("pending", StringComparison.OrdinalIgnoreCase))
            return true;

        var currentIsFinalFailure = currentStatus is "expired" or "cancelled" or "failed";
        return currentIsFinalFailure && nextStatus.Equals("pending", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class LockedOrder
    {
        public long Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }
}
