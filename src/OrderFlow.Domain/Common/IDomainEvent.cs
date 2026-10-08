namespace OrderFlow.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
