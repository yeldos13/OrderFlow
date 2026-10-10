namespace OrderFlow.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public const int MaxErrorLength = 2000;

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public Guid AggregateId { get; private set; }

    public string Type { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public string? TraceParent { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    public static OutboxMessage Create(
        Guid id,
        Guid aggregateId,
        string type,
        string payload,
        string? traceParent,
        DateTimeOffset occurredAt) => new()
        {
            Id = id,
            AggregateId = aggregateId,
            Type = type,
            Payload = payload,
            TraceParent = traceParent,
            OccurredAt = occurredAt
        };

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = Truncate(error);
    }

    public void MarkFailedPermanently(string error, DateTimeOffset now)
    {
        MarkFailed(error);
        FailedAt = now;
    }

    private static string Truncate(string error) =>
        error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
}
