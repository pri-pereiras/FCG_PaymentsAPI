namespace Payments.Application.Abstractions;
public record PaymentGatewayRequest(Guid OrderId, decimal Amount, string Currency);
public record PaymentGatewayResult(bool Approved, string? TransactionId, string? ReasonCode, string? Reason);
public interface IPaymentGateway
{
    Task<PaymentGatewayResult> ProcessAsync(PaymentGatewayRequest request, CancellationToken ct);
}
public sealed class TemporaryGatewayException(string message) : Exception(message);
