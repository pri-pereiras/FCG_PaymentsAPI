using Fcg.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Payments.Application.Options;
using Payments.Application.UseCases.ProcessOrder;
using Payments.Infrastructure.Gateways;
using Payments.Infrastructure.Persistence;
using Xunit;
namespace Payments.Domain.Tests;
public sealed class IntegrationTests
{
    [Theory]
    [InlineData(149.90, "Approved")]
    [InlineData(1500, "Rejected")]
    [InlineData(777.01, "Rejected")]
    public async Task Order_SavesPaymentAttemptsAndOutbox_WithoutDuplicates(double price, string expected)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PaymentsDbContext>().UseSqlite(connection).Options;
        await using var db = new PaymentsDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var repository = new EfPaymentRepository(db);
        var config = Options.Create(new PaymentSimulationOptions { GatewayDelayMilliseconds = 0 });
        var handler = new ProcessOrderHandler(repository, new FakePaymentGateway(config));
        var order = new OrderPlacedEvent(Guid.NewGuid(), 7, 9, (decimal)price);
        await handler.HandleAsync(order, config.Value, order.OrderId, CancellationToken.None);
        await handler.HandleAsync(order, config.Value, order.OrderId, CancellationToken.None);
        Assert.Equal(expected, (await repository.GetByOrderIdAsync(order.OrderId, CancellationToken.None))!.Status);
        Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
        Assert.Equal(1, await db.Attempts.CountAsync());
    }
    [Fact]
    public async Task GatewayTimeout_IsRetryableAndDoesNotCreateFalseRejection()
    {
        var config = Options.Create(new PaymentSimulationOptions { GatewayDelayMilliseconds = 0 });
        await Assert.ThrowsAsync<Payments.Application.Abstractions.TemporaryGatewayException>(async () =>
            await new FakePaymentGateway(config).ProcessAsync(
                new Payments.Application.Abstractions.PaymentGatewayRequest(Guid.NewGuid(), 777.04m, "BRL"), CancellationToken.None));
    }
}
