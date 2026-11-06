using Fcg.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payments.Application.Options;
using Payments.Application.UseCases.ProcessOrder;
namespace Payments.Infrastructure.Messaging;
public sealed class OrderPlacedConsumer(ProcessOrderHandler handler,
    IOptions<PaymentSimulationOptions> options, ILogger<OrderPlacedConsumer> logger) : IConsumer<OrderPlacedEvent>
{
    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var correlationId = context.CorrelationId ?? context.Message.OrderId;
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId, ["OrderId"] = context.Message.OrderId }))
        {
            logger.LogInformation("OrderPlacedEvent recebido. OrderId: {OrderId}, CorrelationId: {CorrelationId}",
                context.Message.OrderId, correlationId);
            try
            {
                await handler.HandleAsync(context.Message, options.Value, correlationId, context.CancellationToken);
                logger.LogInformation("OrderPlacedEvent processado. OrderId: {OrderId}", context.Message.OrderId);
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar OrderPlacedEvent. OrderId: {OrderId}", context.Message.OrderId);
                throw; // Preserva retry e fila _error do MassTransit
            }
        }
    }
}
