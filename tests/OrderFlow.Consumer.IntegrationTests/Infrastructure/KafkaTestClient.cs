using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using OrderFlow.Contracts;
using OrderFlow.Contracts.Orders;

namespace OrderFlow.Consumer.IntegrationTests.Infrastructure;

public sealed record KafkaRecord(string Key, string Value, IReadOnlyDictionary<string, string> Headers);

public static class KafkaTestClient
{
    public static Task PublishEventAsync(string bootstrapServers, IOrderEvent orderEvent) =>
        PublishAsync(
            bootstrapServers,
            orderEvent.OrderId.ToString(),
            JsonSerializer.Serialize(orderEvent, orderEvent.GetType(), EventSerializer.Options),
            new Dictionary<string, string>
            {
                [MessageHeaders.EventId] = orderEvent.EventId.ToString(),
                [MessageHeaders.EventType] = OrderEventTypes.GetName(orderEvent)
            });

    public static async Task PublishAsync(
        string bootstrapServers,
        string key,
        string value,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = bootstrapServers }).Build();

        var kafkaHeaders = new Headers();
        foreach (var (name, headerValue) in headers ?? new Dictionary<string, string>())
        {
            kafkaHeaders.Add(name, Encoding.UTF8.GetBytes(headerValue));
        }

        await producer.ProduceAsync(Topics.Orders, new Message<string, string> { Key = key, Value = value, Headers = kafkaHeaders });
    }

    public static async Task<IReadOnlyList<KafkaRecord>> ReadByKeyAsync(
        string bootstrapServers,
        string topic,
        string key,
        int expectedCount,
        TimeSpan timeout)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = $"integration-tests-reader-{Guid.NewGuid():N}",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            })
            .Build();

        consumer.Subscribe(topic);

        var records = new List<KafkaRecord>();
        var deadline = DateTime.UtcNow + timeout;

        await Task.Run(() =>
        {
            while (records.Count < expectedCount && DateTime.UtcNow < deadline)
            {
                var result = consumer.Consume(TimeSpan.FromMilliseconds(250));
                if (result?.Message is null || result.Message.Key != key)
                {
                    continue;
                }

                records.Add(new KafkaRecord(
                    result.Message.Key,
                    result.Message.Value,
                    result.Message.Headers.ToDictionary(header => header.Key, header => Encoding.UTF8.GetString(header.GetValueBytes()))));
            }
        });

        consumer.Close();
        return records;
    }
}
