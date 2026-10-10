using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Outbox;

internal sealed partial class OutboxCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.CleanupInterval, timeProvider);

        do
        {
            try
            {
                await DeleteProcessedMessagesAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCleanupFailed(exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeleteProcessedMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();

        var threshold = timeProvider.GetUtcNow() - options.Value.ProcessedRetention;

        var deleted = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAt < threshold)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            LogMessagesDeleted(deleted);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} processed outbox messages")]
    private partial void LogMessagesDeleted(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete processed outbox messages")]
    private partial void LogCleanupFailed(Exception exception);
}
