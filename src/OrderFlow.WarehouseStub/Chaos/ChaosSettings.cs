namespace OrderFlow.WarehouseStub.Chaos;

public sealed record ChaosSettings
{
    public const string SectionName = "Chaos";

    public bool Outage { get; init; }

    public double FailureRate { get; init; }

    public double SlowRate { get; init; }

    public int SlowDelayMs { get; init; } = 3000;

    public IEnumerable<string> Validate()
    {
        if (FailureRate is < 0 or > 1)
        {
            yield return "FailureRate must be between 0 and 1.";
        }

        if (SlowRate is < 0 or > 1)
        {
            yield return "SlowRate must be between 0 and 1.";
        }

        if (SlowDelayMs is < 0 or > 60_000)
        {
            yield return "SlowDelayMs must be between 0 and 60000.";
        }
    }
}
