using Fcg.Contracts;
namespace Payments.Application.Abstractions;
public interface IDomainEventPublisher
{
    Task PublishAsync(PaymentProcessedEvent message, CancellationToken ct);
}
