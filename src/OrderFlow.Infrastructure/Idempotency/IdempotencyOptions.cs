namespace OrderFlow.Infrastructure.Idempotency;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public TimeSpan Retention { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(1);
}
