namespace OrderFlow.Infrastructure.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = string.Empty;

    public string ClientId { get; init; } = "orderflow-api";

    public int MessageTimeoutMs { get; init; } = 15_000;
}
