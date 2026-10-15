using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace OrderFlow.Consumer.Health;

internal sealed class ConsumerHealthCheck(
    ConsumerStatus status,
    IOptions<ConsumerHealthOptions> options,
    TimeProvider timeProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = status.GetSnapshot();
        var now = timeProvider.GetUtcNow();

        var data = new Dictionary<string, object>
        {
            ["assignedPartitions"] = snapshot.AssignedPartitions,
            ["lag"] = snapshot.TotalLag,
            ["deadLettered"] = snapshot.DeadLettered
        };

        if (snapshot.LastProgressAt is { } lastProgressAt)
        {
            data["lastProgressAt"] = lastProgressAt;
        }

        if (snapshot.AssignedPartitions.Count == 0)
        {
            return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, "No partitions assigned.", data: data));
        }

        if (snapshot.TotalLag > 0 && snapshot.LastProgressAt is { } progress && now - progress > options.Value.StallThreshold)
        {
            return Task.FromResult(new HealthCheckResult(
                context.Registration.FailureStatus,
                $"Consumer is stalled: lag {snapshot.TotalLag}, no progress for {(now - progress).TotalSeconds:F0}s.",
                data: data));
        }

        if (snapshot.LastDeadLetteredAt is { } deadLetteredAt && now - deadLetteredAt < options.Value.DeadLetterWindow)
        {
            return Task.FromResult(new HealthCheckResult(
                context.Registration.FailureStatus,
                "Messages were moved to the dead letter topic recently.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"Consuming, lag {snapshot.TotalLag}.", data));
    }
}
