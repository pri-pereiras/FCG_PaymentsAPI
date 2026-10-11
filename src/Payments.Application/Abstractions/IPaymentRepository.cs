using Payments.Domain.Entities;
namespace Payments.Application.Abstractions;
public interface IPaymentRepository
{
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid orderId, CancellationToken ct);
    Task AddAsync(Payment payment, CancellationToken ct);
    Task<IReadOnlyList<Payment>> GetPendingEventsAsync(int count, CancellationToken ct);
    Task MarkEventPublishedAsync(Payment payment, CancellationToken ct);
}
