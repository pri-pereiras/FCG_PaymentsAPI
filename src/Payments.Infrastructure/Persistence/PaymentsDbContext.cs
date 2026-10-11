using Microsoft.EntityFrameworkCore;
using Payments.Domain.Entities;
namespace Payments.Infrastructure.Persistence;
public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Payment>();
        entity.HasKey(x => x.OrderId);
        entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
        entity.Property(x => x.Reason).HasMaxLength(200);
    }
}
