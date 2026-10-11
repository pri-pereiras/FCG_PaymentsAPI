using Fcg.Contracts;
using MassTransit;
using Payments.Application.Abstractions;
namespace Payments.Infrastructure.Messaging;
public sealed class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IDomainEventPublisher
{
    public Task PublishAsync(PaymentProcessedEvent message, CancellationToken ct) => publishEndpoint.Publish(message, ct);
}
