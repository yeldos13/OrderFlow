using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Idempotency;
using OrderFlow.Infrastructure.Idempotency;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrderFlow")
            ?? throw new InvalidOperationException("Connection string 'OrderFlow' is not configured.");

        services.TryAddSingleton(TimeProvider.System);

        services.AddDbContext<OrderFlowDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IOrderFlowDbContext>(provider => provider.GetRequiredService<OrderFlowDbContext>());

        services.AddOptions<IdempotencyOptions>()
            .BindConfiguration(IdempotencyOptions.SectionName)
            .Validate(options => options.Retention > TimeSpan.Zero, "Idempotency retention must be positive.")
            .Validate(options => options.CleanupInterval > TimeSpan.Zero, "Idempotency cleanup interval must be positive.")
            .ValidateOnStart();

        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();
        services.AddHostedService<IdempotencyCleanupService>();

        services.AddHealthChecks()
            .AddDbContextCheck<OrderFlowDbContext>("database", tags: [HealthCheckTags.Ready]);

        return services;
    }

    public static async Task ApplyMigrationsAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
