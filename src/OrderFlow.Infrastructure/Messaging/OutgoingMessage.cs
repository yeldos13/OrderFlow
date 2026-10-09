namespace OrderFlow.Infrastructure.Messaging;

public sealed record OutgoingMessage(
    string Topic,
    string Key,
    string Value,
    IReadOnlyDictionary<string, string?> Headers);
