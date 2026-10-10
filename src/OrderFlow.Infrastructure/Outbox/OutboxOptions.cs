using OrderFlow.Contracts;

namespace OrderFlow.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; init; } = 50;

    public string Topic { get; init; } = Topics.Orders;

    public TimeSpan ProcessedRetention { get; init; } = TimeSpan.FromDays(7);

    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(1);

    public TimeSpan MaxLag { get; init; } = TimeSpan.FromMinutes(1);
}
