using Microsoft.EntityFrameworkCore;
using Payments.Application.Abstractions;
using Payments.Domain.Entities;
namespace Payments.Infrastructure.Persistence;
public sealed class EfPaymentRepository(PaymentsDbContext db) : IPaymentRepository
{
    public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct) =>
        db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
    public Task<bool> ExistsAsync(Guid orderId, CancellationToken ct) =>
        db.Payments.AsNoTracking().AnyAsync(x => x.OrderId == orderId, ct);
    public async Task AddAsync(Payment payment, CancellationToken ct)
    {
        db.Payments.Add(payment);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (!await ExistsAsync(payment.OrderId, ct)) throw;
            // Mensagem duplicada tratada como idempotente.
        }
    }
    public async Task<IReadOnlyList<Payment>> GetPendingEventsAsync(int count, CancellationToken ct) =>
        await db.Payments.Where(x => !x.EventPublished).OrderBy(x => x.ProcessedAtUtc)
            .Take(count).ToListAsync(ct);
    public async Task MarkEventPublishedAsync(Payment payment, CancellationToken ct)
    {
        payment.MarkEventPublished();
        await db.SaveChangesAsync(ct);
    }
}
