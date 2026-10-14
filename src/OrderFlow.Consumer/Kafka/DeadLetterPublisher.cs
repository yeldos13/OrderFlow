using System.Globalization;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using OrderFlow.Contracts;

namespace OrderFlow.Consumer.Kafka;

public sealed partial class DeadLetterPublisher : IDisposable
{
    private const int MaxReasonLength = 1000;
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(5);

    private readonly IProducer<string, string> _producer;
    private readonly KafkaConsumerOptions _options;
    private readonly TimeProvider _timeProvider;

    public DeadLetterPublisher(IOptions<KafkaConsumerOptions> options, TimeProvider timeProvider, ILogger<DeadLetterPublisher> logger)
    {
        _options = options.Value;
        _timeProvider = timeProvider;

        _producer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                ClientId = $"{_options.ClientId}-dlq",
                Acks = Acks.All,
                EnableIdempotence = true,
                MessageTimeoutMs = _options.ProducerMessageTimeoutMs
            })
            .SetLogHandler((_, logMessage) => LogKafkaClientMessage(logger, logMessage.Level.ToLogLevel(), logMessage.Facility, logMessage.Message))
            .Build();
    }

    public async Task PublishAsync(ConsumeResult<string, string> result, Exception exception, CancellationToken cancellationToken)
    {
        var headers = new Headers();

        foreach (var header in result.Message.Headers ?? [])
        {
            headers.Add(header.Key, header.GetValueBytes());
        }

        var reason = exception.Message.Length > MaxReasonLength ? exception.Message[..MaxReasonLength] : exception.Message;

        AddHeader(headers, DeadLetterHeaders.Reason, reason);
        AddHeader(headers, DeadLetterHeaders.ExceptionType, exception.GetType().FullName ?? exception.GetType().Name);
        AddHeader(headers, DeadLetterHeaders.OriginalTopic, result.Topic);
        AddHeader(headers, DeadLetterHeaders.OriginalPartition, result.Partition.Value.ToString(CultureInfo.InvariantCulture));
        AddHeader(headers, DeadLetterHeaders.OriginalOffset, result.Offset.Value.ToString(CultureInfo.InvariantCulture));
        AddHeader(headers, DeadLetterHeaders.ConsumerGroup, _options.GroupId);
        AddHeader(headers, DeadLetterHeaders.FailedAt, _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));

        await _producer.ProduceAsync(
            _options.DeadLetterTopic,
            new Message<string, string>
            {
                Key = result.Message.Key,
                Value = result.Message.Value,
                Headers = headers
            },
            cancellationToken);
    }

    public void Dispose()
    {
        _producer.Flush(FlushTimeout);
        _producer.Dispose();
    }

    private static void AddHeader(Headers headers, string name, string value) =>
        headers.Add(name, Encoding.UTF8.GetBytes(value));

    [LoggerMessage(Message = "Kafka dead letter producer {Facility}: {Text}")]
    private static partial void LogKafkaClientMessage(ILogger logger, LogLevel level, string facility, string text);
}
