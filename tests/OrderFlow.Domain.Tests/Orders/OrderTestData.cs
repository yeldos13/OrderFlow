using OrderFlow.Domain.Orders;

namespace OrderFlow.Domain.Tests.Orders;

internal static class OrderTestData
{
    public static readonly DateTimeOffset CreatedAt = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset ChangedAt = CreatedAt.AddHours(2);

    public static OrderLine Line(int quantity = 1, decimal unitPrice = 10m) =>
        new(Guid.NewGuid(), quantity, unitPrice);

    public static Order CreateOrder(params OrderLine[] lines) =>
        Order.Create(Guid.NewGuid(), lines.Length == 0 ? [Line()] : lines, CreatedAt);

    public static Order OrderIn(OrderStatus status)
    {
        var order = CreateOrder();

        switch (status)
        {
            case OrderStatus.Confirmed:
                order.Confirm(CreatedAt);
                break;
            case OrderStatus.Shipped:
                order.Confirm(CreatedAt);
                order.Ship(CreatedAt);
                break;
            case OrderStatus.Cancelled:
                order.Cancel(CreatedAt);
                break;
        }

        order.ClearDomainEvents();
        return order;
    }
}
