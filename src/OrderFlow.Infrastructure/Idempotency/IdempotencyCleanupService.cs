using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Idempotency;

internal sealed partial class IdempotencyCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<IdempotencyOptions> options,
    TimeProvider timeProvider,
    ILogger<IdempotencyCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.CleanupInterval, timeProvider);

        do
        {
            try
            {
                await DeleteExpiredRecordsAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCleanupFailed(exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeleteExpiredRecordsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();

        var threshold = timeProvider.GetUtcNow() - options.Value.Retention;

        var deleted = await dbContext.IdempotencyRecords
            .Where(record => record.CreatedAt < threshold)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            LogRecordsDeleted(deleted);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} expired idempotency records")]
    private partial void LogRecordsDeleted(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete expired idempotency records")]
    private partial void LogCleanupFailed(Exception exception);
}
