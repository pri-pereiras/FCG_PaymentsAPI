using Fcg.Contracts;
using Payments.Application.Abstractions;
using Payments.Domain.Entities;
namespace Payments.Application.UseCases.ProcessOrder;

public sealed class ProcessOrderHandler(IPaymentRepository repository)
{
    public async Task HandleAsync(OrderPlacedEvent order, decimal maximumApprovedAmount, CancellationToken ct)
    {
        if (order.OrderId == Guid.Empty || order.UserId <= 0 || order.GameId <= 0)
            throw new ArgumentException("Pedido inválido.");
        if (await repository.ExistsAsync(order.OrderId, ct)) return;
        var payment = Payment.Process(order.OrderId, order.UserId, order.GameId, order.Price, maximumApprovedAmount);
        await repository.AddAsync(payment, ct);
    }
}
