using Payments.Domain.Entities;
using Payments.Domain.ValueObjects;
using Xunit;
namespace Payments.Domain.Tests;
public sealed class PaymentTests
{
    [Fact]
    public void Lifecycle_Approved_IsValid()
    {
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 1, 2, 149.90m);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        payment.StartProcessing();
        Assert.Equal(PaymentStatus.Processing, payment.Status);
        payment.Finish(true, "SIM-123", null, null, 1);
        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.NotNull(payment.ProcessedAtUtc);
        Assert.Equal(1, payment.AttemptCount);
    }
    [Fact]
    public void Lifecycle_Rejected_StoresFailureCode()
    {
        var payment = Payment.Create(Guid.NewGuid(), Guid.Empty, 1, 2, 550m);
        payment.StartProcessing();
        payment.Finish(false, null, "INSUFFICIENT_FUNDS", "Saldo insuficiente", 1);
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Equal("INSUFFICIENT_FUNDS", payment.FailureCode);
    }
    [Fact]
    public void CannotFinishWithoutProcessing()
    {
        var payment = Payment.Create(Guid.NewGuid(), Guid.Empty, 1, 2, 12m);
        Assert.Throws<InvalidOperationException>(() => payment.Finish(true, null, null, null, 1));
    }
    [Fact]
    public void Money_ValidatesAmounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-1));
        Assert.Throws<ArgumentException>(() => new Money(1.001m));
        Assert.Equal("BRL", new Money(1.50m).Currency);
    }
}
