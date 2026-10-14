using OrderFlow.Contracts;

namespace OrderFlow.Consumer.Kafka;

public sealed class KafkaConsumerOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = string.Empty;

    public string ClientId { get; init; } = "orderflow-consumer";

    public string GroupId { get; init; } = string.Empty;

    public string Topic { get; init; } = Topics.Orders;

    public string DeadLetterTopic { get; init; } = Topics.OrdersDeadLetter;

    public int ProducerMessageTimeoutMs { get; init; } = 15_000;

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
}
