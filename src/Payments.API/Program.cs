using Microsoft.EntityFrameworkCore;
using Payments.Application.Abstractions;
using Payments.Application.UseCases.GetPayment;
using Payments.Infrastructure;
using Payments.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPaymentInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

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
    await db.Database.EnsureCreatedAsync();
}
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapHealthChecks("/health"); // liveness
app.MapGet("/ready", async (PaymentsDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
var paymentEndpoint = app.MapGet("/api/payments/{orderId:guid}", async (Guid orderId, GetPaymentHandler handler, CancellationToken ct) =>
{
    var payment = await handler.HandleAsync(orderId, ct);
    return payment is null ? Results.NotFound() : Results.Ok(new
    {
        payment.PaymentId, payment.OrderId, payment.CorrelationId, payment.UserId, payment.GameId,
        payment.Price, payment.Currency, payment.Status, payment.TransactionId, payment.FailureCode,
        payment.Reason, payment.CreatedAtUtc, payment.ProcessedAtUtc, payment.AttemptCount
    });
}).WithTags("Payments");

if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/payments/{orderId:guid}/attempts", async (Guid orderId, IPaymentRepository repo, CancellationToken ct) =>
        Results.Ok(await repo.GetAttemptsAsync(orderId, ct))).WithTags("Diagnostics");
    app.MapGet("/api/payments/stats", async (IPaymentRepository repo, CancellationToken ct) =>
        Results.Ok(await repo.GetStatisticsAsync(ct))).WithTags("Diagnostics");
    app.MapGet("/dashboard", () => Results.Content("""
        <!doctype html><html lang="pt-br"><meta charset="utf-8"><title>FCG Payments</title>
        <style>body{font:16px system-ui;max-width:880px;margin:48px auto;padding:0 20px;background:#111827;color:#f3f4f6}h1{color:#93c5fd}.cards{display:flex;gap:14px;flex-wrap:wrap}.card{border:1px solid #374151;padding:22px;border-radius:12px;min-width:140px;background:#1f2937}strong{display:block;font-size:30px}small{color:#9ca3af}</style>
        <h1>FCG • Payments dashboard</h1><p>Informações de pagamentos simulados. Atualização a cada 10 segundos.</p>
        <div id="cards" class="cards">Carregando...</div>
        <script>async function refresh(){try{let r=await fetch('/api/payments/stats'); if(!r.ok)throw Error(r.status);let d=await r.json();document.getElementById('cards').innerHTML=Object.entries(d).map(([k,v])=>'<div class="card"><small>'+k+'</small><strong>'+v+'</strong></div>').join('')}catch(e){document.getElementById('cards').textContent='Erro: '+e.message}}refresh();setInterval(refresh,10000);</script>
        </html>
        """, "text/html")).WithTags("Diagnostics");
}
app.Run();
public partial class Program { }
