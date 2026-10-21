using Microsoft.Extensions.Options;
using Payments.Application.Abstractions;
using Payments.Application.Options;
namespace Payments.Infrastructure.Gateways;
/// <summary>Previsível e sem dados bancários reais. Valores especiais servem apenas para testes.</summary>
public sealed class FakePaymentGateway(IOptions<PaymentSimulationOptions> options) : IPaymentGateway
{
    public async Task<PaymentGatewayResult> ProcessAsync(PaymentGatewayRequest request, CancellationToken ct)
    {
        var o = options.Value;
        await Task.Delay(Math.Max(0, o.GatewayDelayMilliseconds), ct);
        if (request.Amount == 777.03m) throw new TemporaryGatewayException("Gateway indisponível (cenário de teste).");
        if (request.Amount == 777.04m) throw new TemporaryGatewayException("Timeout simulado do gateway.");
        if (request.Amount == 777.01m) return new(false, null, "INSUFFICIENT_FUNDS", "Saldo insuficiente (simulado).");
        if (request.Amount == 777.02m) return new(false, null, "INVALID_CARD", "Cartão inválido (simulado).");
        if (request.Amount > o.MaximumApprovedAmount) return new(false, null, "LIMIT_EXCEEDED", "Valor acima do limite de simulação.");
        return new(true, $"SIM-{request.OrderId:N}", null, null);
    }
}
