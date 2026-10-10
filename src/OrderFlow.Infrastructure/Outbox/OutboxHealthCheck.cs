using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Outbox;

internal sealed class OutboxHealthCheck(
    OrderFlowDbContext dbContext,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var oldestPending = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAt == null && message.FailedAt == null)
            .MinAsync(message => (DateTimeOffset?)message.OccurredAt, cancellationToken);

        var failedCount = await dbContext.OutboxMessages
            .CountAsync(message => message.FailedAt != null, cancellationToken);

        var lag = oldestPending is null ? TimeSpan.Zero : timeProvider.GetUtcNow() - oldestPending.Value;

        var data = new Dictionary<string, object>
        {
            ["lagSeconds"] = Math.Round(lag.TotalSeconds, 1),
            ["failedMessages"] = failedCount
        };

        if (lag > options.Value.MaxLag)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, $"Oldest pending outbox message is {lag.TotalSeconds:F0}s old.", data: data);
        }

        if (failedCount > 0)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, $"{failedCount} outbox messages were rejected by the broker.", data: data);
        }

        return HealthCheckResult.Healthy("Outbox is being processed.", data);
    }
}
