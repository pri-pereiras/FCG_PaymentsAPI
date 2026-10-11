using Microsoft.EntityFrameworkCore;
using Payments.Application.UseCases.GetPayment;
using Payments.Infrastructure;
using Payments.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPaymentInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    var connection = db.Database.GetConnectionString();
    if (connection?.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) == true)
    {
        var path = connection["Data Source=".Length..];
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    }
    await db.Database.EnsureCreatedAsync(); // MVP; migrations recomendadas para produção.
}
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "PaymentsAPI" }));
app.MapGet("/api/payments/{orderId:guid}", async (Guid orderId, GetPaymentHandler handler, CancellationToken ct) =>
{
    var payment = await handler.HandleAsync(orderId, ct);
    return payment is null ? Results.NotFound() : Results.Ok(new
    {
        payment.OrderId, payment.UserId, payment.GameId, payment.Price,
        payment.Status, payment.Reason, payment.ProcessedAtUtc
    });
});
app.Run();
public partial class Program { }
