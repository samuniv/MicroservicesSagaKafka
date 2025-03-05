using PaymentService.Domain.Models;

namespace PaymentService.Domain.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id);
    Task<Payment?> GetByOrderIdAsync(Guid orderId);
    Task<Payment?> GetByTransactionIdAsync(string transactionId);
    Task<IEnumerable<Payment>> GetAllAsync();
    Task CreateAsync(Payment payment);
    Task UpdateAsync(Payment payment);
    Task<bool> ExistsAsync(Guid id);
    Task SaveChangesAsync();
} 