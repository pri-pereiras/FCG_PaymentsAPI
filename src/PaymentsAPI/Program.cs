using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentsAPI.Consumers;
using PaymentsAPI.Data;
using PaymentsAPI.Options;
using PaymentsAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<PaymentOptions>()
    .Bind(builder.Configuration.GetSection("PaymentSimulation"))
    .Validate(o => o.MaximumApprovedAmount >= 0, "MaximumApprovedAmount deve ser >= 0")
    .ValidateOnStart();

builder.Services.AddDbContext<PaymentsDbContext>(o => o.UseSqlite(
    builder.Configuration.GetConnectionString("PaymentsDb")
    ?? throw new InvalidOperationException("ConnectionStrings:PaymentsDb não configurada")));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderPlacedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });
        cfg.ReceiveEndpoint(builder.Configuration["RabbitMq:OrderQueue"] ?? "payments-order-placed", e =>
        {
            e.ConfigureConsumer<OrderPlacedConsumer>(context);
            e.UseMessageRetry(r => r.Intervals(500, 1000, 3000));
            e.PrefetchCount = 8;
        });
    });
});

builder.Services.AddHostedService<PaymentEventDispatcher>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    // MVP: cria o banco na primeira execução. Para produção, substituir por migrations.
    var connectionString = db.Database.GetConnectionString();
    if (connectionString?.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) == true)
    {
        var databaseFile = connectionString["Data Source=".Length..];
        var directory = Path.GetDirectoryName(Path.GetFullPath(databaseFile));
        if (directory is not null) Directory.CreateDirectory(directory);
    }
    await db.Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "PaymentsAPI" }));
app.MapGet("/api/payments/{orderId:guid}", async (Guid orderId, PaymentsDbContext db) =>
{
    var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == orderId);
    return payment is null ? Results.NotFound() : Results.Ok(new
    {
        payment.OrderId, payment.UserId, payment.GameId,
        payment.Price, payment.Status, payment.Reason, payment.ProcessedAtUtc
    });
});
app.Run();

public partial class Program { }
