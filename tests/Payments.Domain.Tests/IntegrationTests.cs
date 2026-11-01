
using Fcg.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Payments.Application.Abstractions;
using Payments.Application.Options;
using Payments.Application.UseCases.ProcessOrder;
using Payments.Infrastructure.Gateways;
using Payments.Infrastructure.Persistence;
using Xunit;

namespace Payments.Domain.Tests;

public sealed class IntegrationTests
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

    [Theory]
    [InlineData(149.90, "Approved")]
    [InlineData(499.99, "Approved")]
    [InlineData(500.00, "Rejected")]
    [InlineData(550.00, "Rejected")]
    [InlineData(700.00, "Rejected")]
    [InlineData(750.00, "Rejected")]
    [InlineData(1000.00, "Rejected")]
    [InlineData(1500.00, "Rejected")]
    public async Task Order_ShouldPersistWithoutDuplicates(
        double price,
        string expectedStatus)
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var dbOptions =
            new DbContextOptionsBuilder<PaymentsDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var db =
            new PaymentsDbContext(dbOptions);

        await db.Database.EnsureCreatedAsync();

        var repository = new EfPaymentRepository(db);
        var config = CreateOptions();

        var gateway = new FakePaymentGateway(
            Options.Create(config));

        var handler = new ProcessOrderHandler(
            repository, gateway);

        var order = new OrderPlacedEvent(
            Guid.NewGuid(),
            7,
            9,
            (decimal)price);

        // Primeira tentativa de processamento
        await handler.HandleAsync(
            order,
            config,
            order.OrderId,
            CancellationToken.None);

        // Mesmo pedido enviado novamente
        await handler.HandleAsync(
            order,
            config,
            order.OrderId,
            CancellationToken.None);

        var payment =
            await repository.GetByOrderIdAsync(
                order.OrderId,
                CancellationToken.None);

        Assert.NotNull(payment);
        Assert.Equal(
            expectedStatus,
            payment.Status);

        Assert.Equal(1, payment.AttemptCount);

        // Não pode duplicar o pagamento
        Assert.Equal(
            1,
            await db.Payments.CountAsync());

        // Não pode duplicar a mensagem Outbox
        Assert.Equal(
            1,
            await db.OutboxMessages.CountAsync());

        // Uma tentativa bem-sucedida ou rejeitada
        Assert.Equal(
            1,
            await db.Attempts.CountAsync());
    }

    [Theory]
    [InlineData(800)]
    [InlineData(850)]
    [InlineData(900)]
    [InlineData(950)]
    public async Task TemporaryFailure_ShouldNotPersistFalseRejection(
        double price)
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync();

        var dbOptions =
            new DbContextOptionsBuilder<PaymentsDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var db =
            new PaymentsDbContext(dbOptions);

        await db.Database.EnsureCreatedAsync();

        var repository = new EfPaymentRepository(db);
        var config = CreateOptions();

        var gateway = new FakePaymentGateway(
            Options.Create(config));

        var handler = new ProcessOrderHandler(
            repository, gateway);

        var order = new OrderPlacedEvent(
            Guid.NewGuid(),
            7,
            9,
            (decimal)price);

        await Assert.ThrowsAsync<TemporaryGatewayException>(
            () => handler.HandleAsync(
                order,
                config,
                order.OrderId,
                CancellationToken.None));

        // Não registra um pagamento rejeitado
        // quando houve apenas falha técnica.
        Assert.Equal(
            0,
            await db.Payments.CountAsync());

        Assert.Equal(
            0,
            await db.OutboxMessages.CountAsync());
    }
}
