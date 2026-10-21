
using Microsoft.Extensions.Options;
using Payments.Application.Abstractions;
using Payments.Application.Options;

namespace Payments.Infrastructure.Gateways;

public sealed class FakePaymentGateway(
    IOptions<PaymentSimulationOptions> options)
    : IPaymentGateway
{
    public async Task<PaymentGatewayResult> ProcessAsync(
        PaymentGatewayRequest request,
        CancellationToken ct)
    {
        var config = options.Value;

        await Task.Delay(
            config.GatewayDelayMilliseconds, ct);

        if (request.Amount <= 0)
        {
            return new PaymentGatewayResult(
                false, null,
                "INVALID_AMOUNT",
                "Valor inválido.");
        }

        // Localiza a faixa que corresponde ao valor.
        var rule = config.Rules.SingleOrDefault(
            x => request.Amount >= x.MinAmount &&
                 request.Amount <= x.MaxAmount);

        if (rule is null)
        {
            return new PaymentGatewayResult(
                false, null,
                "NO_MATCHING_RULE",
                "Nenhuma regra de simulação encontrada.");
        }

        // Executa o cenário definido na configuração.
        return rule.Scenario switch
        {
            "Approved" => new PaymentGatewayResult(
                true,
                $"SIM-{request.OrderId:N}",
                null,
                null),

            "InsufficientFunds" => new PaymentGatewayResult(
                false,
                null,
                "INSUFFICIENT_FUNDS",
                "Saldo insuficiente (simulado)."),

            "InvalidCard" => new PaymentGatewayResult(
                false,
                null,
                "INVALID_CARD",
                "Cartão inválido (simulado)."),

            "LimitExceeded" => new PaymentGatewayResult(
                false,
                null,
                "LIMIT_EXCEEDED",
                "Limite excedido (simulado)."),

            "GatewayUnavailable" => throw
                new TemporaryGatewayException(
                    "Gateway temporariamente indisponível."),

            "Timeout" => throw
                new TemporaryGatewayException(
                    "Timeout simulado no gateway."),

            _ => throw new InvalidOperationException(
                $"Cenário desconhecido: {rule.Scenario}")
        };
    }
}
