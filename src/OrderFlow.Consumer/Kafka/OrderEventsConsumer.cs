using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using OrderFlow.Consumer.Processing;
using OrderFlow.Contracts;
using Polly.CircuitBreaker;

namespace OrderFlow.Consumer.Kafka;

internal sealed partial class OrderEventsConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaConsumerOptions> options,
    ILogger<OrderEventsConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(
            () => ConsumeLoopAsync(stoppingToken),
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        using var consumer = BuildConsumer();
        consumer.Subscribe(options.Value.Topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message is null)
                {
                    continue;
                }

                await ProcessAsync(consumer, result, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessAsync(IConsumer<string, string> consumer, ConsumeResult<string, string> result, CancellationToken stoppingToken)
    {
        var message = ToReceivedMessage(result);
        using var activity = StartActivity(message);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<OrderEventHandler>();

            await handler.HandleAsync(message, stoppingToken);

            consumer.StoreOffset(result);
        }
        catch (UnsupportedMessageException exception)
        {
            LogMessageSkipped(exception, result.TopicPartitionOffset.ToString());
            consumer.StoreOffset(result);
        }
        catch (BrokenCircuitException exception)
        {
            var pause = exception.RetryAfter ?? options.Value.RetryDelay;
            LogCircuitOpen(result.TopicPartitionOffset.ToString(), pause.TotalSeconds);
            consumer.Seek(result.TopicPartitionOffset);
            await Task.Delay(pause, stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            LogProcessingFailed(exception, result.TopicPartitionOffset.ToString());
            consumer.Seek(result.TopicPartitionOffset);
            await Task.Delay(options.Value.RetryDelay, stoppingToken);
        }
    }

    private IConsumer<string, string> BuildConsumer()
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            ClientId = options.Value.ClientId,
            GroupId = options.Value.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky
        };

        return new ConsumerBuilder<string, string>(config)
            .SetLogHandler((_, logMessage) => LogKafkaClientMessage(ToLogLevel(logMessage.Level), logMessage.Facility, logMessage.Message))
            .SetPartitionsAssignedHandler((_, partitions) => LogPartitionsAssigned(string.Join(", ", partitions.Select(partition => partition.Partition.Value))))
            .SetPartitionsRevokedHandler((_, partitions) => LogPartitionsRevoked(string.Join(", ", partitions.Select(partition => partition.Partition.Value))))
            .Build();
    }

    private static ReceivedMessage ToReceivedMessage(ConsumeResult<string, string> result) => new(
        result.Message.Key,
        result.Message.Value,
        GetHeader(result.Message.Headers, MessageHeaders.EventId),
        GetHeader(result.Message.Headers, MessageHeaders.EventType),
        GetHeader(result.Message.Headers, MessageHeaders.TraceParent));

    private static string? GetHeader(Headers? headers, string name) =>
        headers is not null && headers.TryGetLastBytes(name, out var value) ? Encoding.UTF8.GetString(value) : null;

    private static Activity StartActivity(ReceivedMessage message)
    {
        var activity = new Activity("ProcessOrderEvent");

        if (message.TraceParent is not null)
        {
            activity.SetParentId(message.TraceParent);
        }

        return activity.Start();
    }

    private static LogLevel ToLogLevel(SyslogLevel level) => level switch
    {
        SyslogLevel.Emergency or SyslogLevel.Alert or SyslogLevel.Critical => LogLevel.Critical,
        SyslogLevel.Error => LogLevel.Error,
        SyslogLevel.Warning => LogLevel.Warning,
        SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
        _ => LogLevel.Debug
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Partitions assigned: {Partitions}")]
    private partial void LogPartitionsAssigned(string partitions);

    [LoggerMessage(Level = LogLevel.Information, Message = "Partitions revoked: {Partitions}")]
    private partial void LogPartitionsRevoked(string partitions);

    [LoggerMessage(Level = LogLevel.Error, Message = "Skipping unsupported message at {Position}")]
    private partial void LogMessageSkipped(Exception exception, string position);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Warehouse circuit is open, pausing consumption at {Position} for {PauseSeconds:F1} s")]
    private partial void LogCircuitOpen(string position, double pauseSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process message at {Position}, it will be retried")]
    private partial void LogProcessingFailed(Exception exception, string position);

    [LoggerMessage(Message = "Kafka client {Facility}: {Text}")]
    private partial void LogKafkaClientMessage(LogLevel level, string facility, string text);
}
