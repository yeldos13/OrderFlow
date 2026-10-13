namespace OrderFlow.Consumer.Warehouse;

public sealed class WarehouseOptions
{
    public const string SectionName = "Warehouse";

    public Uri? BaseUrl { get; init; }

    public WarehouseResilienceOptions Resilience { get; init; } = new();
}

public sealed class WarehouseResilienceOptions
{
    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public int MaxRetryAttempts { get; init; } = 4;

    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    public double CircuitFailureRatio { get; init; } = 0.5;

    public int CircuitMinimumThroughput { get; init; } = 10;

    public TimeSpan CircuitSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan CircuitBreakDuration { get; init; } = TimeSpan.FromSeconds(15);
}
