namespace OrderFlow.Consumer.Deduplication;

public sealed class DeduplicationOptions
{
    public const string SectionName = "Deduplication";

    public TimeSpan Retention { get; init; } = TimeSpan.FromDays(14);

    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(1);
}
