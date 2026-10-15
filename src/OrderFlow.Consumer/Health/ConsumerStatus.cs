namespace OrderFlow.Consumer.Health;

public sealed class ConsumerStatus(TimeProvider timeProvider)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, long> _lagByPartition = [];
    private DateTimeOffset? _lastProgressAt;
    private DateTimeOffset? _lastDeadLetteredAt;
    private long _deadLettered;

    public void PartitionsAssigned(IEnumerable<int> partitions)
    {
        lock (_lock)
        {
            foreach (var partition in partitions)
            {
                _lagByPartition.TryAdd(partition, 0);
            }

            _lastProgressAt ??= timeProvider.GetUtcNow();
        }
    }

    public void PartitionsRevoked(IEnumerable<int> partitions)
    {
        lock (_lock)
        {
            foreach (var partition in partitions)
            {
                _lagByPartition.Remove(partition);
            }
        }
    }

    public void MessageReceived(int partition, long lag)
    {
        lock (_lock)
        {
            _lagByPartition[partition] = lag;
        }
    }

    public void MessageCompleted(int partition, long lag)
    {
        lock (_lock)
        {
            _lagByPartition[partition] = lag;
            _lastProgressAt = timeProvider.GetUtcNow();
        }
    }

    public void MessageDeadLettered(int partition, long lag)
    {
        lock (_lock)
        {
            _lagByPartition[partition] = lag;
            _lastProgressAt = timeProvider.GetUtcNow();
            _lastDeadLetteredAt = _lastProgressAt;
            _deadLettered++;
        }
    }

    public ConsumerStatusSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new ConsumerStatusSnapshot(
                [.. _lagByPartition.Keys.Order()],
                _lagByPartition.Values.Sum(),
                _lastProgressAt,
                _deadLettered,
                _lastDeadLetteredAt);
        }
    }
}

public sealed record ConsumerStatusSnapshot(
    IReadOnlyList<int> AssignedPartitions,
    long TotalLag,
    DateTimeOffset? LastProgressAt,
    long DeadLettered,
    DateTimeOffset? LastDeadLetteredAt);
