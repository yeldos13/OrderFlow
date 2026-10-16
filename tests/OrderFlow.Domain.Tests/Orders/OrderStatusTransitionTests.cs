using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Orders.Events;
using static OrderFlow.Domain.Tests.Orders.OrderTestData;

namespace OrderFlow.Domain.Tests.Orders;

public sealed class OrderStatusTransitionTests
{
    public enum Transition
    {
        Confirm,
        Ship,
        Cancel
    }

    public static TheoryData<OrderStatus, Transition, OrderStatus, Type> AllowedTransitions => new()
    {
        { OrderStatus.Created, Transition.Confirm, OrderStatus.Confirmed, typeof(OrderConfirmedDomainEvent) },
        { OrderStatus.Created, Transition.Cancel, OrderStatus.Cancelled, typeof(OrderCancelledDomainEvent) },
        { OrderStatus.Confirmed, Transition.Ship, OrderStatus.Shipped, typeof(OrderShippedDomainEvent) },
        { OrderStatus.Confirmed, Transition.Cancel, OrderStatus.Cancelled, typeof(OrderCancelledDomainEvent) }
    };

    public static TheoryData<OrderStatus, Transition, OrderStatus> ForbiddenTransitions => new()
    {
        { OrderStatus.Created, Transition.Ship, OrderStatus.Shipped },
        { OrderStatus.Confirmed, Transition.Confirm, OrderStatus.Confirmed },
        { OrderStatus.Shipped, Transition.Confirm, OrderStatus.Confirmed },
        { OrderStatus.Shipped, Transition.Ship, OrderStatus.Shipped },
        { OrderStatus.Shipped, Transition.Cancel, OrderStatus.Cancelled },
        { OrderStatus.Cancelled, Transition.Confirm, OrderStatus.Confirmed },
        { OrderStatus.Cancelled, Transition.Ship, OrderStatus.Shipped },
        { OrderStatus.Cancelled, Transition.Cancel, OrderStatus.Cancelled }
    };

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void AllowedTransition_ChangesStatusAndRaisesEvent(
        OrderStatus initial,
        Transition transition,
        OrderStatus expected,
        Type expectedEvent)
    {
        var order = OrderIn(initial);

        Apply(order, transition);

        Assert.Equal(expected, order.Status);
        Assert.Equal(ChangedAt, order.UpdatedAt);
        Assert.Equal(CreatedAt, order.CreatedAt);

        var domainEvent = Assert.Single(order.DomainEvents);
        Assert.IsType(expectedEvent, domainEvent);
        Assert.Equal(ChangedAt, domainEvent.OccurredAt);
    }

    [Theory]
    [MemberData(nameof(ForbiddenTransitions))]
    public void ForbiddenTransition_ThrowsAndKeepsState(OrderStatus initial, Transition transition, OrderStatus target)
    {
        var order = OrderIn(initial);

        var exception = Assert.Throws<InvalidOrderStateException>(() => Apply(order, transition));

        Assert.Equal(order.Id, exception.OrderId);
        Assert.Equal(initial, exception.CurrentStatus);
        Assert.Equal(target, exception.TargetStatus);
        Assert.Equal(initial, order.Status);
        Assert.Equal(CreatedAt, order.UpdatedAt);
        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public void FullLifecycle_RaisesEventsInOrder()
    {
        var order = CreateOrder();

        order.Confirm(ChangedAt);
        order.Ship(ChangedAt);

        Assert.Collection(
            order.DomainEvents,
            domainEvent => Assert.IsType<OrderCreatedDomainEvent>(domainEvent),
            domainEvent => Assert.IsType<OrderConfirmedDomainEvent>(domainEvent),
            domainEvent => Assert.IsType<OrderShippedDomainEvent>(domainEvent));
    }

    private static void Apply(Order order, Transition transition)
    {
        switch (transition)
        {
            case Transition.Confirm:
                order.Confirm(ChangedAt);
                break;
            case Transition.Ship:
                order.Ship(ChangedAt);
                break;
            case Transition.Cancel:
                order.Cancel(ChangedAt);
                break;
        }
    }
}
