using Payments.Domain.Entities;
using Xunit;
namespace Payments.Domain.Tests;
public sealed class PaymentTests
{
    [Theory]
    [InlineData(100, "Approved")]
    [InlineData(1000, "Approved")]
    [InlineData(1000.01, "Rejected")]
    [InlineData(-1, "Rejected")]
    public void Process_AppliesSimulationRule(double price, string expected)
    {
        var payment = Payment.Process(Guid.NewGuid(), 1, 2, (decimal)price, 1000);
        Assert.Equal(expected, payment.Status);
        Assert.False(payment.EventPublished);
        payment.MarkEventPublished();
        Assert.True(payment.EventPublished);
    }
}
