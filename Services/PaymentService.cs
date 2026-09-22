using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Repositories;

namespace MyKicksBuddy.Services;

// Intentionally not registered until a real provider adapter is configured.
public sealed class PaymentService
{
    private readonly IOrderRepository _orders;
    private readonly IPaymentRepository _payments;
    private readonly IPaymentProvider _provider;

    public PaymentService(IOrderRepository orders, IPaymentRepository payments, IPaymentProvider provider) =>
        (_orders, _payments, _provider) = (orders, payments, provider);

    public async Task<Payment> CreateForCustomerAsync(long customerId, long orderId)
    {
        var order = await _orders.GetDetailByIdAndCustomerAsync(orderId, customerId)
            ?? throw new UnauthorizedAccessException("Pesanan tidak ditemukan untuk akun ini.");
        if (order.Status != OrderStatusWorkflow.PendingPayment || order.PaymentStatus != PaymentStatusWorkflow.Unpaid)
            throw new InvalidOperationException("Pesanan tidak menunggu pembayaran.");
        if (order.TotalAmount <= 0)
            throw new InvalidOperationException("Total pembayaran tidak valid.");
        var existing = await _payments.GetByOrderIdAsync(orderId);
        if (existing is not null) return existing;

        var creation = await _provider.CreateAsync(order.OrderCode, order.TotalAmount, "IDR", order.OrderCode);
        if (string.IsNullOrWhiteSpace(creation.Provider) || string.IsNullOrWhiteSpace(creation.ProviderReference))
            throw new InvalidOperationException("Referensi pembayaran penyedia tidak valid.");
        return await _payments.CreateAsync(orderId, creation, order.TotalAmount);
    }

    public async Task<bool> HandleNotificationAsync(string rawBody, IReadOnlyDictionary<string, string> headers)
    {
        var verified = await _provider.VerifyNotificationAsync(rawBody, headers)
            ?? throw new UnauthorizedAccessException("Notifikasi pembayaran tidak terverifikasi.");
        return await _payments.ApplyVerifiedResultAsync(verified);
    }
}
