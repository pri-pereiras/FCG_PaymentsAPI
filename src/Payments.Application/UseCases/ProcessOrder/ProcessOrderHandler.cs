using System.Text.Json;
using Fcg.Contracts;
using Payments.Application.Abstractions;
using Payments.Application.Options;
using Payments.Domain.Entities;
namespace Payments.Application.UseCases.ProcessOrder;
public sealed class ProcessOrderHandler(IPaymentRepository repository, IPaymentGateway gateway)
{
    public async Task HandleAsync(OrderPlacedEvent order, PaymentSimulationOptions options, Guid correlationId, CancellationToken ct)
    {
        if (order.OrderId == Guid.Empty || order.UserId <= 0 || order.GameId <= 0 || order.Price < 0 || decimal.Round(order.Price, 2) != order.Price)
            throw new ArgumentException("Pedido inválido.");
        // Chave única em OrderId também protege gravações concorrentes.
        if (await repository.GetByOrderIdAsync(order.OrderId, ct) is not null) return;
        var payment = Payment.Create(order.OrderId, correlationId, order.UserId, order.GameId, order.Price);
        payment.StartProcessing();
        var attempts = new List<PaymentAttempt>();
        PaymentGatewayResult? result = null;
        for (var i = 1; i <= Math.Max(1, options.MaximumAttempts); i++)
        {
            try
            {
                result = await gateway.ProcessAsync(new PaymentGatewayRequest(order.OrderId, order.Price, "BRL"), ct);
                attempts.Add(PaymentAttempt.Create(order.OrderId, i, result.Approved ? "Approved" : "Rejected", result.ReasonCode));
                break;
            }
            catch (TemporaryGatewayException ex)
            {
                attempts.Add(PaymentAttempt.Create(order.OrderId, i, "TemporaryFailure", ex.Message));
                if (i == Math.Max(1, options.MaximumAttempts)) throw; // RabbitMQ fará retry / fila _error.
                await Task.Delay(TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds * i), ct);
            }
        }
        if (result is null) throw new InvalidOperationException("Gateway não retornou um resultado.");
        payment.Finish(result.Approved, result.TransactionId, result.ReasonCode, result.Reason, attempts.Count);
        // Contrato externo permanece compatível com CatalogAPI / NotificationsAPI.
        var integrationEvent = new PaymentProcessedEvent(payment.OrderId, payment.UserId, payment.GameId,
            payment.Price, payment.Status, payment.ProcessedAtUtc!.Value, payment.Reason);
        await repository.SaveResultAsync(payment, attempts, JsonSerializer.Serialize(integrationEvent), ct);
    }
}
