using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrderFlow.Infrastructure.Messaging;

internal sealed partial class KafkaMessagePublisher : IMessagePublisher, IDisposable
{
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(5);

    private readonly IProducer<string, string> _producer;

    public KafkaMessagePublisher(IOptions<KafkaOptions> options, ILogger<KafkaMessagePublisher> logger)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            ClientId = options.Value.ClientId,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = options.Value.MessageTimeoutMs,
            LingerMs = 5
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetLogHandler((_, logMessage) => LogKafkaClientMessage(logger, ToLogLevel(logMessage.Level), logMessage.Facility, logMessage.Message))
            .Build();
    }

    public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        var headers = new Headers();

        foreach (var (name, value) in message.Headers)
        {
            if (value is not null)
            {
                headers.Add(name, Encoding.UTF8.GetBytes(value));
            }
        }

        try
        {
            await _producer.ProduceAsync(
                message.Topic,
                new Message<string, string>
                {
                    Key = message.Key,
                    Value = message.Value,
                    Headers = headers
                },
                cancellationToken);
        }
        catch (ProduceException<string, string> exception) when (IsPermanent(exception.Error.Code))
        {
            throw new PermanentPublishException(exception.Error.Reason, exception);
        }
    }

    public void Dispose()
    {
        _producer.Flush(FlushTimeout);
        _producer.Dispose();
    }

    private static bool IsPermanent(ErrorCode code) => code is
        ErrorCode.MsgSizeTooLarge or
        ErrorCode.InvalidMsg or
        ErrorCode.InvalidMsgSize or
        ErrorCode.InvalidRecord or
        ErrorCode.Local_KeySerialization or
        ErrorCode.Local_ValueSerialization;

    private static LogLevel ToLogLevel(SyslogLevel level) => level switch
    {
        SyslogLevel.Emergency or SyslogLevel.Alert or SyslogLevel.Critical => LogLevel.Critical,
        SyslogLevel.Error => LogLevel.Error,
        SyslogLevel.Warning => LogLevel.Warning,
        SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
        _ => LogLevel.Debug
    };

    [LoggerMessage(Message = "Kafka client {Facility}: {Text}")]
    private static partial void LogKafkaClientMessage(ILogger logger, LogLevel level, string facility, string text);
}
