using Microsoft.EntityFrameworkCore;
using Payments.Application.Abstractions;
using Payments.Domain.Entities;
namespace Payments.Infrastructure.Persistence;
public sealed class EfPaymentRepository(PaymentsDbContext db) : IPaymentRepository
{
    public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct) =>
        db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
    public async Task SaveResultAsync(Payment payment, IReadOnlyList<PaymentAttempt> attempts, string eventPayload, CancellationToken ct)
    {
        // Payment + tentativas + outbox são gravados atomicamente em uma transação SQLite.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.Payments.Add(payment);
            db.Attempts.AddRange(attempts);
            db.OutboxMessages.Add(OutboxMessage.Create(payment.OrderId, eventPayload));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            if (await GetByOrderIdAsync(payment.OrderId, ct) is null) throw;
            // Apenas se outra execução já inseriu o mesmo pedido: idempotente.
        }
    }
    public async Task<IReadOnlyList<OutboxMessage>> GetPendingEventsAsync(int count, CancellationToken ct) =>
        await db.OutboxMessages.Where(x => x.PublishedAtUtc == null).OrderBy(x => x.CreatedAtUtc).Take(count).ToListAsync(ct);
    public async Task MarkPublishedAsync(OutboxMessage message, CancellationToken ct)
    { message.Published(); await db.SaveChangesAsync(ct); }
    public async Task MarkFailedAsync(OutboxMessage message, string error, CancellationToken ct)
    { message.Failed(error); await db.SaveChangesAsync(ct); }
    public async Task<PaymentStatistics> GetStatisticsAsync(CancellationToken ct)
    {
        var total = await db.Payments.CountAsync(ct);
        var approved = await db.Payments.CountAsync(x => x.Status == PaymentStatus.Approved, ct);
        var rejected = await db.Payments.CountAsync(x => x.Status == PaymentStatus.Rejected, ct);
        var pending = await db.Payments.CountAsync(x => x.Status == PaymentStatus.Pending, ct);
        var processing = await db.Payments.CountAsync(x => x.Status == PaymentStatus.Processing, ct);
        var amounts = await db.Payments.Where(x => x.Status == PaymentStatus.Approved)
            .Select(x => x.Price).ToListAsync(ct);
        var approvedAmount = amounts.Sum();
        return new(total, approved, rejected, pending, processing, approvedAmount);
    }
    public async Task<IReadOnlyList<PaymentAttempt>> GetAttemptsAsync(Guid orderId, CancellationToken ct) =>
        await db.Attempts.AsNoTracking().Where(x => x.OrderId == orderId).OrderBy(x => x.Number).ToListAsync(ct);
}
