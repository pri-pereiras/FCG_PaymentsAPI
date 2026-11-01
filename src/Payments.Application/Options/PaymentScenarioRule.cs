
namespace Payments.Application.Options;

public sealed class PaymentScenarioRule
{
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public string Scenario { get; set; } = string.Empty;
}
