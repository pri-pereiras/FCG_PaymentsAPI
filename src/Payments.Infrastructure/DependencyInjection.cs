using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payments.Application.Abstractions;
using Payments.Application.Options;
using Payments.Application.UseCases.GetPayment;
using Payments.Application.UseCases.ProcessOrder;
using Payments.Infrastructure.Gateways;
using Payments.Infrastructure.Messaging;
using Payments.Infrastructure.Persistence;
namespace Payments.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddPaymentInfrastructure(this IServiceCollection services, IConfiguration config)
    {

        services.AddOptions<PaymentSimulationOptions>()
            .Bind(config.GetSection("PaymentSimulation"))
            .Validate(o =>
            {
                if (o.MaximumAttempts <= 0 ||
                    o.GatewayDelayMilliseconds < 0 ||
                    o.RetryDelayMilliseconds < 0 ||
                    o.Rules.Count == 0)
                {
                    return false;
                }

                var validScenarios = new HashSet<string>(
                    StringComparer.Ordinal)
                {
            "Approved",
            "InsufficientFunds",
            "InvalidCard",
            "GatewayUnavailable",
            "Timeout",
            "LimitExceeded"
                };

                var rules = o.Rules
                    .OrderBy(r => r.MinAmount)
                    .ToList();

                for (int i = 0; i < rules.Count; i++)
                {
                    var current = rules[i];

                    if (current.MinAmount < 0.01m ||
                        current.MaxAmount < current.MinAmount ||
                        decimal.Round(current.MinAmount, 2)
                            != current.MinAmount ||
                        decimal.Round(current.MaxAmount, 2)
                            != current.MaxAmount ||
                        !validScenarios.Contains(current.Scenario))
                    {
                        return false;
                    }

                    // Impede faixas sobrepostas.
                    if (i > 0 &&
                        current.MinAmount <= rules[i - 1].MaxAmount)
                    {
                        return false;
                    }
                }

                return true;
            }, "Regras de simulação inválidas ou sobrepostas.")
            .ValidateOnStart();
        services.AddDbContext<PaymentsDbContext>(o => o.UseSqlite(config.GetConnectionString("PaymentsDb")
            ?? throw new InvalidOperationException("ConnectionStrings:PaymentsDb não configurada")));
        services.AddScoped<IPaymentRepository, EfPaymentRepository>();
        services.AddScoped<IPaymentGateway, FakePaymentGateway>();
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
