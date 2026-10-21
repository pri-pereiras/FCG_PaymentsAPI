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
            await handler.HandleAsync(context.Message, options.Value, correlationId, context.CancellationToken);
            logger.LogInformation("OrderPlacedEvent processado: {OrderId}", context.Message.OrderId);
        }
    }
}
