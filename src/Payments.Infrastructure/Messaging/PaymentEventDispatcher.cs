using System.Text.Json;
using Fcg.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payments.Application.Abstractions;
namespace Payments.Infrastructure.Messaging;
// Outbox at-least-once; para replicação horizontal, adicione claim/lease com controle de concorrência.
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
                foreach (var item in await repository.GetPendingEventsAsync(50, stoppingToken))
                {
                    try
                    {
                        var message = JsonSerializer.Deserialize<PaymentProcessedEvent>(item.Payload)
                            ?? throw new InvalidOperationException("Evento inválido na Outbox.");
                        using (logger.BeginScope(new Dictionary<string, object> { ["OrderId"] = item.OrderId }))
                        {
                            await publisher.PublishAsync(message, stoppingToken);
                            await repository.MarkPublishedAsync(item, stoppingToken);
                            logger.LogInformation("PaymentProcessedEvent publicado: {OrderId}", item.OrderId);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Falha ao entregar Outbox para pedido {OrderId}", item.OrderId);
                        await repository.MarkFailedAsync(item, ex.Message, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Falha no dispatcher da Outbox"); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
