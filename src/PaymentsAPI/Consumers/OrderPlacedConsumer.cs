using Fcg.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentsAPI.Data;
using PaymentsAPI.Options;

namespace PaymentsAPI.Consumers;

public sealed class OrderPlacedConsumer(
    PaymentsDbContext db,
    IOptions<PaymentOptions> options,
    ILogger<OrderPlacedConsumer> logger) : IConsumer<OrderPlacedEvent>
{
    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var order = context.Message;
        if (order.OrderId == Guid.Empty || order.UserId <= 0 || order.GameId <= 0)
            throw new ArgumentException("OrderId, UserId e GameId devem ser válidos.");

        // Chave única no banco: uma redelivery não pode criar novo pagamento para a mesma compra.
        if (await db.Payments.AnyAsync(p => p.OrderId == order.OrderId, context.CancellationToken))
        {
            logger.LogInformation("Compra {OrderId} já processada: ignorando entrega duplicada", order.OrderId);
            return;
        }

        // Simulação determinística: sem gateway ou cobrança real.
        var approved = order.Price >= 0 && order.Price <= options.Value.MaximumApprovedAmount;
        var reason = approved ? null : order.Price < 0
            ? "Valor inválido" : "Pagamento recusado pela regra de simulação";

        db.Payments.Add(new PaymentRecord
        {
            OrderId = order.OrderId,
            UserId = order.UserId,
            GameId = order.GameId,
            Price = order.Price,
            Status = approved ? "Approved" : "Rejected",
            ProcessedAtUtc = DateTime.UtcNow,
            Reason = reason,
            EventPublished = false
        });
        try
        {
            await db.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException)
        {
            // Uma corrida entre entregas duplicadas pode violar a chave única.
            db.ChangeTracker.Clear();
            if (!await db.Payments.AsNoTracking()
                .AnyAsync(p => p.OrderId == order.OrderId, context.CancellationToken))
                throw;
            logger.LogInformation("Pagamento {OrderId} persistido por outro consumidor", order.OrderId);
            return;
        }
        logger.LogInformation("Pagamento {OrderId}: {Status}", order.OrderId, approved ? "Approved" : "Rejected");
    }
}
