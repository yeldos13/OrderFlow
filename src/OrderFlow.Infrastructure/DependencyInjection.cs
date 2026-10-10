using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Idempotency;
using OrderFlow.Infrastructure.Idempotency;
using OrderFlow.Infrastructure.Messaging;
using OrderFlow.Infrastructure.Outbox;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrderFlow")
            ?? throw new InvalidOperationException("Connection string 'OrderFlow' is not configured.");

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<OutboxInterceptor>();

        services.AddDbContext<OrderFlowDbContext>((provider, options) => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(provider.GetRequiredService<OutboxInterceptor>()));

        services.AddScoped<IOrderFlowDbContext>(provider => provider.GetRequiredService<OrderFlowDbContext>());

        services.AddOptions<IdempotencyOptions>()
            .BindConfiguration(IdempotencyOptions.SectionName)
            .Validate(options => options.Retention > TimeSpan.Zero, "Idempotency retention must be positive.")
            .Validate(options => options.CleanupInterval > TimeSpan.Zero, "Idempotency cleanup interval must be positive.")
            .ValidateOnStart();

        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();
        services.AddHostedService<IdempotencyCleanupService>();

        services.AddOptions<KafkaOptions>()
            .BindConfiguration(KafkaOptions.SectionName)
            .Validate(options => !string.IsNullOrWhiteSpace(options.BootstrapServers), "Kafka bootstrap servers are not configured.")
            .Validate(options => options.MessageTimeoutMs > 0, "Kafka message timeout must be positive.")
            .ValidateOnStart();

        services.AddSingleton<IMessagePublisher, KafkaMessagePublisher>();

        services.AddOptions<OutboxOptions>()
            .BindConfiguration(OutboxOptions.SectionName)
            .Validate(options => options.PollingInterval > TimeSpan.Zero, "Outbox polling interval must be positive.")
            .Validate(options => options.BatchSize > 0, "Outbox batch size must be positive.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Topic), "Outbox topic is not configured.")
            .Validate(options => options.ProcessedRetention > TimeSpan.Zero, "Outbox retention must be positive.")
            .Validate(options => options.CleanupInterval > TimeSpan.Zero, "Outbox cleanup interval must be positive.")
            .Validate(options => options.MaxLag > TimeSpan.Zero, "Outbox max lag must be positive.")
            .ValidateOnStart();

        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<OutboxCleanupService>();

        services.AddSingleton<KafkaHealthCheck>();

        services.AddHealthChecks()
            .AddDbContextCheck<OrderFlowDbContext>("database", tags: [HealthCheckTags.Ready])
            .AddCheck<KafkaHealthCheck>("kafka", HealthStatus.Degraded, [HealthCheckTags.Messaging])
            .AddCheck<OutboxHealthCheck>("outbox", HealthStatus.Degraded, [HealthCheckTags.Messaging]);

        return services;
    }

    public static async Task ApplyMigrationsAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
