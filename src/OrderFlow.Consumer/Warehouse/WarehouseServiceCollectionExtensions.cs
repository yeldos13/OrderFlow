using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace OrderFlow.Consumer.Warehouse;

public static class WarehouseServiceCollectionExtensions
{
    public static IServiceCollection AddWarehouseClient(this IServiceCollection services)
    {
        services.AddOptions<WarehouseOptions>()
            .BindConfiguration(WarehouseOptions.SectionName)
            .Validate(options => options.BaseUrl is { IsAbsoluteUri: true }, "Warehouse base URL is not configured.")
            .Validate(options => options.Resilience.AttemptTimeout > TimeSpan.Zero, "Attempt timeout must be positive.")
            .Validate(options => options.Resilience.TotalTimeout > options.Resilience.AttemptTimeout, "Total timeout must be greater than attempt timeout.")
            .Validate(options => options.Resilience.MaxRetryAttempts >= 0, "Retry attempts must not be negative.")
            .Validate(options => options.Resilience.CircuitFailureRatio is > 0 and <= 1, "Circuit failure ratio must be in (0, 1].")
            .Validate(options => options.Resilience.CircuitSamplingDuration >= options.Resilience.AttemptTimeout * 2, "Circuit sampling duration must be at least twice the attempt timeout.")
            .ValidateOnStart();

        services.AddHttpClient<WarehouseClient>((provider, client) =>
            {
                client.BaseAddress = provider.GetRequiredService<IOptions<WarehouseOptions>>().Value.BaseUrl;
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddResilienceHandler("warehouse", (pipeline, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<WarehouseOptions>>().Value.Resilience;

                pipeline
                    .AddTimeout(options.TotalTimeout)
                    .AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = options.MaxRetryAttempts,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = options.RetryBaseDelay
                    })
                    .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                    {
                        FailureRatio = options.CircuitFailureRatio,
                        MinimumThroughput = options.CircuitMinimumThroughput,
                        SamplingDuration = options.CircuitSamplingDuration,
                        BreakDuration = options.CircuitBreakDuration
                    })
                    .AddTimeout(options.AttemptTimeout);
            });

        return services;
    }
}
