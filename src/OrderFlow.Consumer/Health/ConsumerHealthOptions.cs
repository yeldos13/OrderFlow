namespace OrderFlow.Consumer.Health;

public sealed class ConsumerHealthOptions
{
    public const string SectionName = "Health";

    public TimeSpan StallThreshold { get; init; } = TimeSpan.FromMinutes(1);

    public TimeSpan DeadLetterWindow { get; init; } = TimeSpan.FromMinutes(15);
}
