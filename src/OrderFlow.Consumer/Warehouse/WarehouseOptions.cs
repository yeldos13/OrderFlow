namespace OrderFlow.Consumer.Warehouse;

public sealed class WarehouseOptions
{
    public const string SectionName = "Warehouse";

    public Uri? BaseUrl { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
