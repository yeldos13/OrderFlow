namespace OrderFlow.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal TotalAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public static Order Create(Guid customerId, IReadOnlyCollection<OrderLine> lines, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (customerId == Guid.Empty)
        {
            throw new OrderValidationException("Customer id is required.");
        }

        if (lines.Count == 0)
        {
            throw new OrderValidationException("Order must contain at least one item.");
        }

        var duplicateProductId = lines
            .GroupBy(line => line.ProductId)
            .Where(group => group.Count() > 1)
            .Select(group => (Guid?)group.Key)
            .FirstOrDefault();

        if (duplicateProductId is not null)
        {
            throw new OrderValidationException($"Product {duplicateProductId} appears in the order more than once.");
        }

        var order = new Order
        {
            Id = Guid.CreateVersion7(now),
            CustomerId = customerId,
            Status = OrderStatus.Created,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var line in lines)
        {
            order._items.Add(new OrderItem(line.ProductId, line.Quantity, line.UnitPrice));
        }

        order.TotalAmount = order._items.Sum(item => item.LineTotal);

        return order;
    }

    public void Confirm(DateTimeOffset now) => TransitionTo(OrderStatus.Confirmed, now);

    public void Ship(DateTimeOffset now) => TransitionTo(OrderStatus.Shipped, now);

    public void Cancel(DateTimeOffset now) => TransitionTo(OrderStatus.Cancelled, now);

    private void TransitionTo(OrderStatus targetStatus, DateTimeOffset now)
    {
        if (!CanTransition(Status, targetStatus))
        {
            throw new InvalidOrderStateException(Id, Status, targetStatus);
        }

        Status = targetStatus;
        UpdatedAt = now;
    }

    private static bool CanTransition(OrderStatus from, OrderStatus to) => (from, to) switch
    {
        (OrderStatus.Created, OrderStatus.Confirmed) => true,
        (OrderStatus.Created, OrderStatus.Cancelled) => true,
        (OrderStatus.Confirmed, OrderStatus.Shipped) => true,
        (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
        _ => false
    };
}
