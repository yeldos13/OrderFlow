using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using OrderFlow.Consumer.Health;
using OrderFlow.Consumer.Processing;
using OrderFlow.Consumer.Warehouse;
using OrderFlow.Contracts;
using Polly.CircuitBreaker;

namespace OrderFlow.Consumer.Kafka;

internal sealed partial class OrderEventsConsumer(
    IServiceScopeFactory scopeFactory,
    DeadLetterPublisher deadLetterPublisher,
    ConsumerStatus status,
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
        status.MessageReceived(result.Partition.Value, GetLag(consumer, result) + 1);
        var message = ToReceivedMessage(result);
        using var activity = StartActivity(message);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<OrderEventHandler>();

            await handler.HandleAsync(message, stoppingToken);

            consumer.StoreOffset(result);
            status.MessageCompleted(result.Partition.Value, GetLag(consumer, result));
        }
        catch (Exception exception) when (exception is UnsupportedMessageException or WarehouseRejectedException)
        {
            await MoveToDeadLetterAsync(consumer, result, exception, stoppingToken);
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

    private async Task MoveToDeadLetterAsync(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string> result,
        Exception exception,
        CancellationToken stoppingToken)
    {
        try
        {
            await deadLetterPublisher.PublishAsync(result, exception, stoppingToken);
            consumer.StoreOffset(result);
            status.MessageDeadLettered(result.Partition.Value, GetLag(consumer, result));
            LogMovedToDeadLetter(exception, result.TopicPartitionOffset.ToString(), options.Value.DeadLetterTopic);
        }
        catch (Exception publishException) when (publishException is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            LogDeadLetterFailed(publishException, result.TopicPartitionOffset.ToString());
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
            .SetLogHandler((_, logMessage) => LogKafkaClientMessage(logMessage.Level.ToLogLevel(), logMessage.Facility, logMessage.Message))
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                var assigned = partitions.Select(partition => partition.Partition.Value).ToList();
                status.PartitionsAssigned(assigned);
                LogPartitionsAssigned(string.Join(", ", assigned));
            })
            .SetPartitionsRevokedHandler((_, partitions) =>
            {
                var revoked = partitions.Select(partition => partition.Partition.Value).ToList();
                status.PartitionsRevoked(revoked);
                LogPartitionsRevoked(string.Join(", ", revoked));
            })
            .Build();
    }

    private static long GetLag(IConsumer<string, string> consumer, ConsumeResult<string, string> result)
    {
        var high = consumer.GetWatermarkOffsets(result.TopicPartition).High;
        return high == Offset.Unset ? 0 : Math.Max(0, high.Value - result.Offset.Value - 1);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Partitions assigned: {Partitions}")]
    private partial void LogPartitionsAssigned(string partitions);

    [LoggerMessage(Level = LogLevel.Information, Message = "Partitions revoked: {Partitions}")]
    private partial void LogPartitionsRevoked(string partitions);

    [LoggerMessage(Level = LogLevel.Error, Message = "Message at {Position} cannot be processed and was moved to {DeadLetterTopic}")]
    private partial void LogMovedToDeadLetter(Exception exception, string position, string deadLetterTopic);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to move message at {Position} to the dead letter topic, it will be retried")]
    private partial void LogDeadLetterFailed(Exception exception, string position);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Warehouse circuit is open, pausing consumption at {Position} for {PauseSeconds:F1} s")]
    private partial void LogCircuitOpen(string position, double pauseSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process message at {Position}, it will be retried")]
    private partial void LogProcessingFailed(Exception exception, string position);

    [LoggerMessage(Message = "Kafka client {Facility}: {Text}")]
    private partial void LogKafkaClientMessage(LogLevel level, string facility, string text);
}
