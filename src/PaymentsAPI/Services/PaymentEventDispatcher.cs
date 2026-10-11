using Fcg.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentsAPI.Data;

namespace PaymentsAPI.Services;

// Outbox simples: um resultado salvo não se perde quando o broker fica indisponível.
// A publicação é at-least-once: consumidores devem ser idempotentes por OrderId.
public sealed class PaymentEventDispatcher(
    IServiceScopeFactory scopes,
    ILogger<PaymentEventDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
                var pending = await db.Payments
                    .Where(p => !p.EventPublished)
                    .OrderBy(p => p.ProcessedAtUtc)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                foreach (var payment in pending)
                {
                    await publisher.Publish(new PaymentProcessedEvent(
                        payment.OrderId,
                        payment.UserId,
                        payment.GameId,
                        payment.Price,
                        payment.Status,
                        payment.ProcessedAtUtc,
                        payment.Reason), stoppingToken);
                    payment.EventPublished = true;
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("PaymentProcessedEvent publicado para {OrderId}", payment.OrderId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao publicar resultados pendentes; nova tentativa em breve");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
