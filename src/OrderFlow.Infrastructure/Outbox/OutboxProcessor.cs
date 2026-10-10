using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderFlow.Contracts;
using OrderFlow.Infrastructure.Messaging;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Outbox;

internal sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IMessagePublisher publisher,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollingInterval, timeProvider);

        do
        {
            try
            {
                int handled;
                do
                {
                    handled = await ProcessBatchAsync(stoppingToken);
                }
                while (handled == options.Value.BatchSize);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogProcessingFailed(exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var batchSize = options.Value.BatchSize;
        var messages = await dbContext.OutboxMessages
            .FromSql($"""
                SELECT * FROM outbox_messages
                WHERE processed_at IS NULL AND failed_at IS NULL
                ORDER BY occurred_at, id
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        var published = 0;
        var rejected = 0;

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(ToOutgoingMessage(message), cancellationToken);
                message.MarkProcessed(timeProvider.GetUtcNow());
                published++;
            }
            catch (PermanentPublishException exception)
            {
                message.MarkFailedPermanently(exception.Message, timeProvider.GetUtcNow());
                LogPublishRejected(exception, message.Id, message.Type);
                rejected++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.MarkFailed(exception.Message);
                LogPublishFailed(exception, message.Id, message.Type, message.Attempts);
                break;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (published > 0)
        {
            LogBatchPublished(published);
        }

        return published + rejected;
    }

    private OutgoingMessage ToOutgoingMessage(OutboxMessage message) => new(
        options.Value.Topic,
        message.AggregateId.ToString(),
        message.Payload,
        new Dictionary<string, string?>
        {
            [MessageHeaders.EventId] = message.Id.ToString(),
            [MessageHeaders.EventType] = message.Type,
            [MessageHeaders.TraceParent] = message.TraceParent
        });

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {Count} outbox messages")]
    private partial void LogBatchPublished(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to publish outbox message {MessageId} of type {MessageType}, attempt {Attempt}")]
    private partial void LogPublishFailed(Exception exception, Guid messageId, string messageType, int attempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} of type {MessageType} was rejected by the broker and will not be retried")]
    private partial void LogPublishRejected(Exception exception, Guid messageId, string messageType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox processing failed")]
    private partial void LogProcessingFailed(Exception exception);
}
