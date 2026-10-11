using Fcg.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payments.Application.Abstractions;
namespace Payments.Infrastructure.Messaging;
// Outbox simplificado: publicação at-least-once. Consumidores devem deduplicar por OrderId.
public sealed class PaymentEventDispatcher(IServiceScopeFactory scopes, ILogger<PaymentEventDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
                var publisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();
                foreach (var payment in await repository.GetPendingEventsAsync(50, stoppingToken))
                {
                    await publisher.PublishAsync(new PaymentProcessedEvent(payment.OrderId, payment.UserId,
                        payment.GameId, payment.Price, payment.Status, payment.ProcessedAtUtc, payment.Reason), stoppingToken);
                    await repository.MarkEventPublishedAsync(payment, stoppingToken);
                    logger.LogInformation("PaymentProcessedEvent publicado: {OrderId}", payment.OrderId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Falha no dispatcher; tentando novamente"); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
