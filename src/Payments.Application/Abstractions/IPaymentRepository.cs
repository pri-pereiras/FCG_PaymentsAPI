using Payments.Domain.Entities;
namespace Payments.Application.Abstractions;
public interface IPaymentRepository
{
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct);
    Task SaveResultAsync(Payment payment, IReadOnlyList<PaymentAttempt> attempts, string eventPayload, CancellationToken ct);
    Task<IReadOnlyList<OutboxMessage>> GetPendingEventsAsync(int count, CancellationToken ct);
    Task MarkPublishedAsync(OutboxMessage message, CancellationToken ct);
    Task MarkFailedAsync(OutboxMessage message, string error, CancellationToken ct);
    Task<PaymentStatistics> GetStatisticsAsync(CancellationToken ct);
    Task<IReadOnlyList<PaymentAttempt>> GetAttemptsAsync(Guid orderId, CancellationToken ct);
}
public record PaymentStatistics(int Total, int Approved, int Rejected, int Pending, int Processing, decimal ApprovedAmount);
