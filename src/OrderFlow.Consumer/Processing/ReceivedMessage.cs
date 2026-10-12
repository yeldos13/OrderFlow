namespace OrderFlow.Consumer.Processing;

public sealed record ReceivedMessage(
    string? Key,
    string Value,
    string? EventId,
    string? EventType,
    string? TraceParent);
