using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrderFlow.ServiceDefaults;

public sealed class KafkaHealthCheck : IHealthCheck, IDisposable
{
    private static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(3);

    private readonly IAdminClient _adminClient;

    public KafkaHealthCheck(string bootstrapServers, string clientId)
    {
        _adminClient = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = bootstrapServers,
                ClientId = $"{clientId}-health"
            })
            .SetLogHandler((_, _) => { })
            .Build();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await Task.Run(() => _adminClient.GetMetadata(MetadataTimeout), cancellationToken);

            return metadata.Brokers.Count > 0
                ? HealthCheckResult.Healthy($"{metadata.Brokers.Count} broker(s) available.")
                : new HealthCheckResult(context.Registration.FailureStatus, "No Kafka brokers available.");
        }
        catch (KafkaException exception)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Kafka is unreachable.", exception);
        }
    }

    public void Dispose() => _adminClient.Dispose();
}
