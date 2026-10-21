using Microsoft.EntityFrameworkCore;
using Payments.Domain.Entities;
namespace Payments.Infrastructure.Persistence;
public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAttempt> Attempts => Set<PaymentAttempt>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(x => x.OrderId);
            e.HasIndex(x => x.PaymentId).IsUnique();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Reason).HasMaxLength(300);
            e.Property(x => x.FailureCode).HasMaxLength(80);
            e.Property(x => x.TransactionId).HasMaxLength(100);
        });
        modelBuilder.Entity<PaymentAttempt>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrderId, x.Number }).IsUnique();
            e.Property(x => x.Outcome).HasMaxLength(40);
        });
        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId).IsUnique();
            e.Property(x => x.Payload).IsRequired();
        });
    }
}
