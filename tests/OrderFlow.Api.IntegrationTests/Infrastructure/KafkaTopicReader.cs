using System.Text;
using Confluent.Kafka;

namespace OrderFlow.Api.IntegrationTests.Infrastructure;

public sealed record KafkaRecord(string Key, string Value, int Partition, IReadOnlyDictionary<string, string> Headers);

public static class KafkaTopicReader
{
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
                GroupId = $"integration-tests-{Guid.NewGuid():N}",
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
                    result.Partition.Value,
                    result.Message.Headers.ToDictionary(header => header.Key, header => Encoding.UTF8.GetString(header.GetValueBytes()))));
            }
        });

        consumer.Close();
        return records;
    }
}
