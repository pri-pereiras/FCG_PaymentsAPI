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
        await handler.HandleAsync(context.Message, options.Value.MaximumApprovedAmount, context.CancellationToken);
        logger.LogInformation("Pedido {OrderId} recebido/processado", context.Message.OrderId);
    }
}
