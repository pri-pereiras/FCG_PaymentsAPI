namespace Payments.Application.Options;
public sealed class PaymentSimulationOptions
{
    public decimal MaximumApprovedAmount { get; set; } = 1000;
    public int GatewayDelayMilliseconds { get; set; } = 150;
    public int MaximumAttempts { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 250;
}
