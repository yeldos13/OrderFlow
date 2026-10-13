using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderFlow.Consumer.Persistence;

namespace OrderFlow.Consumer.Deduplication;

internal sealed partial class ProcessedEventsCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<DeduplicationOptions> options,
    TimeProvider timeProvider,
    ILogger<ProcessedEventsCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.CleanupInterval, timeProvider);

        do
        {
            try
            {
                await DeleteExpiredAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCleanupFailed(exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeleteExpiredAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConsumerDbContext>();

        var threshold = timeProvider.GetUtcNow() - options.Value.Retention;

        var deleted = await dbContext.ProcessedEvents
            .Where(processedEvent => processedEvent.ProcessedAt < threshold)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            LogRecordsDeleted(deleted);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} expired processed event records")]
    private partial void LogRecordsDeleted(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete expired processed event records")]
    private partial void LogCleanupFailed(Exception exception);
}
