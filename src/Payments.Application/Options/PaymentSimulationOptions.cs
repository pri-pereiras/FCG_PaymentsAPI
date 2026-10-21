
namespace Payments.Application.Options;

public sealed class PaymentSimulationOptions
{
    public int GatewayDelayMilliseconds { get; set; } = 150;
    public int MaximumAttempts { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 250;
    public List<PaymentScenarioRule> Rules { get; set; } = [];
}
