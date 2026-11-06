using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Payments.API.Logging;

public static class SerilogConfiguration
{
    public static IHostBuilder UsePaymentsLogging(this IHostBuilder host) =>
        host.UseSerilog((context, logger) =>
        {
            logger.MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("MassTransit", LogEventLevel.Warning)
                .MinimumLevel.Override("RabbitMQ", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "FCG_PaymentsAPI")
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);

            if (context.HostingEnvironment.IsDevelopment())
                logger.WriteTo.Console();
            else
                logger.WriteTo.Console(new CompactJsonFormatter());
        });

    public static IApplicationBuilder UsePaymentsRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpContext, elapsed, exception) =>
            {
                if (exception != null || httpContext.Response.StatusCode >= 500)
                    return LogEventLevel.Error;
                if (httpContext.Response.StatusCode >= 400)
                    return LogEventLevel.Warning;
                if (httpContext.Request.Path.StartsWithSegments("/health") ||
                    httpContext.Request.Path.StartsWithSegments("/ready"))
                    return LogEventLevel.Debug;
                return LogEventLevel.Information;
            };
        });
}
