using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Abstractions;
using Payments.Application.Options;
using Payments.Application.UseCases.GetPayment;
using Payments.Application.UseCases.ProcessOrder;
using Payments.Infrastructure.Messaging;
using Payments.Infrastructure.Persistence;
namespace Payments.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddPaymentInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<PaymentSimulationOptions>()
            .Bind(config.GetSection("PaymentSimulation"))
            .Validate(o => o.MaximumApprovedAmount >= 0, "MaximumApprovedAmount deve ser >= 0")
            .ValidateOnStart();
        services.AddDbContext<PaymentsDbContext>(o => o.UseSqlite(config.GetConnectionString("PaymentsDb")
            ?? throw new InvalidOperationException("ConnectionStrings:PaymentsDb não configurada")));
        services.AddScoped<IPaymentRepository, EfPaymentRepository>();
        services.AddScoped<ProcessOrderHandler>();
        services.AddScoped<GetPaymentHandler>();
        services.AddScoped<IDomainEventPublisher, MassTransitEventPublisher>();
        services.AddMassTransit(x =>
        {
            x.AddConsumer<OrderPlacedConsumer>();
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(config["RabbitMq:Host"] ?? "localhost", "/", h =>
                {
                    h.Username(config["RabbitMq:Username"] ?? "guest");
                    h.Password(config["RabbitMq:Password"] ?? "guest");
                });
                cfg.ReceiveEndpoint(config["RabbitMq:OrderQueue"] ?? "payments-order-placed", e =>
                {
                    e.UseMessageRetry(r => r.Intervals(500, 1000, 3000));
                    e.PrefetchCount = 8;
                    e.ConfigureConsumer<OrderPlacedConsumer>(context);
                });
            });
        });
        services.AddHostedService<PaymentEventDispatcher>();
        return services;
    }
}
