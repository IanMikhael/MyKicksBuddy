using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> GetByOrderIdAsync(long orderId);
    Task<Payment> CreateAsync(long orderId, PaymentCreation creation, decimal expectedAmount);
    Task<bool> ApplyVerifiedResultAsync(VerifiedPaymentResult result);
}
