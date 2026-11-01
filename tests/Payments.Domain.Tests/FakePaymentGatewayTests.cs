
using Microsoft.Extensions.Options;
using Payments.Application.Abstractions;
using Payments.Application.Options;
using Payments.Infrastructure.Gateways;
using Xunit;

namespace Payments.Domain.Tests;

public sealed class FakePaymentGatewayTests
{
    private static PaymentSimulationOptions CreateOptions()
    {
        return new PaymentSimulationOptions
        {
            GatewayDelayMilliseconds = 0,
            MaximumAttempts = 3,
            RetryDelayMilliseconds = 0,
            Rules =
            [
                new()
                {
                    MinAmount = 0.01m,
                    MaxAmount = 499.99m,
                    Scenario = "Approved"
                },
                new()
                {
                    MinAmount = 500m,
                    MaxAmount = 699.99m,
                    Scenario = "InsufficientFunds"
                },
                new()
                {
                    MinAmount = 700m,
                    MaxAmount = 799.99m,
                    Scenario = "InvalidCard"
                },
                new()
                {
                    MinAmount = 800m,
                    MaxAmount = 899.99m,
                    Scenario = "GatewayUnavailable"
                },
                new()
                {
                    MinAmount = 900m,
                    MaxAmount = 999.99m,
                    Scenario = "Timeout"
                },
                new()
                {
                    MinAmount = 1000m,
                    MaxAmount = 999999.99m,
                    Scenario = "LimitExceeded"
                }
            ]
        };
    }

    private static FakePaymentGateway CreateGateway()
    {
        return new FakePaymentGateway(
            Options.Create(CreateOptions()));
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(149.90)]
    [InlineData(499.99)]
    public async Task ValidAmounts_ShouldBeApproved(
        double amount)
    {
        var gateway = CreateGateway();
        var orderId = Guid.NewGuid();

        var result = await gateway.ProcessAsync(
            new PaymentGatewayRequest(
                orderId, (decimal)amount, "BRL"),
            CancellationToken.None);

        Assert.True(result.Approved);
        Assert.Null(result.ReasonCode);
        Assert.Equal(
            $"SIM-{orderId:N}",
            result.TransactionId);
    }

    [Theory]
    [InlineData(500, "INSUFFICIENT_FUNDS")]
    [InlineData(550, "INSUFFICIENT_FUNDS")]
    [InlineData(699.99, "INSUFFICIENT_FUNDS")]
    [InlineData(700, "INVALID_CARD")]
    [InlineData(750, "INVALID_CARD")]
    [InlineData(799.99, "INVALID_CARD")]
    [InlineData(1000, "LIMIT_EXCEEDED")]
    [InlineData(1500, "LIMIT_EXCEEDED")]
    public async Task RejectedAmounts_ShouldReturnReasonCode(
        double amount,
        string expectedCode)
    {
        var gateway = CreateGateway();

        var result = await gateway.ProcessAsync(
            new PaymentGatewayRequest(
                Guid.NewGuid(),
                (decimal)amount,
                "BRL"),
            CancellationToken.None);

        Assert.False(result.Approved);
        Assert.Equal(expectedCode, result.ReasonCode);
        Assert.Null(result.TransactionId);
    }

    [Theory]
    [InlineData(800)]
    [InlineData(850)]
    [InlineData(899.99)]
    [InlineData(900)]
    [InlineData(950)]
    [InlineData(999.99)]
    public async Task TemporaryFailures_ShouldThrowException(
        double amount)
    {
        var gateway = CreateGateway();

        await Assert.ThrowsAsync<TemporaryGatewayException>(
            () => gateway.ProcessAsync(
                new PaymentGatewayRequest(
                    Guid.NewGuid(),
                    (decimal)amount,
                    "BRL"),
                CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task InvalidAmounts_ShouldBeRejected(
        double amount)
    {
        var gateway = CreateGateway();

        var result = await gateway.ProcessAsync(
            new PaymentGatewayRequest(
                Guid.NewGuid(),
                (decimal)amount,
                "BRL"),
            CancellationToken.None);

        Assert.False(result.Approved);
        Assert.Equal("INVALID_AMOUNT", result.ReasonCode);
    }

    [Fact]
    public async Task SameOrder_ShouldGenerateSameTransactionId()
    {
        var gateway = CreateGateway();
        var orderId = Guid.NewGuid();

        var request = new PaymentGatewayRequest(
            orderId, 149.90m, "BRL");

        var first = await gateway.ProcessAsync(
            request, CancellationToken.None);

        var second = await gateway.ProcessAsync(
            request, CancellationToken.None);

        Assert.Equal(
            first.TransactionId,
            second.TransactionId);
    }

    [Fact]
    public async Task AmountOutsideRanges_ShouldBeRejected()
    {
        var gateway = CreateGateway();

        var result = await gateway.ProcessAsync(
            new PaymentGatewayRequest(
                Guid.NewGuid(),
                1000000m,
                "BRL"),
            CancellationToken.None);

        Assert.False(result.Approved);
        Assert.Equal(
            "NO_MATCHING_RULE",
            result.ReasonCode);
    }
}
