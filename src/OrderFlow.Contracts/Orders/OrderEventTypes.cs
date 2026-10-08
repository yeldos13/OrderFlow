namespace OrderFlow.Contracts.Orders;

public static class OrderEventTypes
{
    public const string Created = "order.created.v1";
    public const string Confirmed = "order.confirmed.v1";
    public const string Shipped = "order.shipped.v1";
    public const string Cancelled = "order.cancelled.v1";

    private static readonly Dictionary<string, Type> TypesByName = new()
    {
        [Created] = typeof(OrderCreated),
        [Confirmed] = typeof(OrderConfirmed),
        [Shipped] = typeof(OrderShipped),
        [Cancelled] = typeof(OrderCancelled)
    };

    private static readonly Dictionary<Type, string> NamesByType =
        TypesByName.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static bool TryGetType(string eventType, out Type type) => TypesByName.TryGetValue(eventType, out type!);

    public static string GetName(IOrderEvent orderEvent) => NamesByType[orderEvent.GetType()];
}
