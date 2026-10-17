using System.Text.Json;
using OrderFlow.Contracts;
using OrderFlow.Contracts.Orders;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Orders.Events;
using OrderFlow.Infrastructure.Outbox;

namespace OrderFlow.Infrastructure.Tests.Outbox;

public sealed class OutboxMessageFactoryTests
{
    private const string TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    private static readonly DateTimeOffset OccurredAt = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [Fact]
    public void Create_ForOrderCreated_MapsToContract()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var line = new OrderLine(Guid.NewGuid(), 2, 10.5m);

        var message = OutboxMessageFactory.Create(
            new OrderCreatedDomainEvent(orderId, customerId, 21m, [line], OccurredAt),
            TraceParent);

        Assert.Equal(OrderEventTypes.Created, message.Type);
        Assert.Equal(orderId, message.AggregateId);
        Assert.Equal(OccurredAt, message.OccurredAt);
        Assert.Equal(TraceParent, message.TraceParent);
        Assert.Null(message.ProcessedAt);
        Assert.Equal(0, message.Attempts);

        var contract = JsonSerializer.Deserialize<OrderCreated>(message.Payload, EventSerializer.Options)!;
        Assert.Equal(message.Id, contract.EventId);
        Assert.Equal(orderId, contract.OrderId);
        Assert.Equal(customerId, contract.CustomerId);
        Assert.Equal(21m, contract.TotalAmount);
        Assert.Equal(OccurredAt, contract.OccurredAt);
        Assert.Equal(new OrderCreatedItem(line.ProductId, 2, 10.5m), Assert.Single(contract.Items));
    }

    public static TheoryData<IDomainEvent, string, Type> StatusEvents()
    {
        var orderId = Guid.NewGuid();

        return new TheoryData<IDomainEvent, string, Type>
        {
            { new OrderConfirmedDomainEvent(orderId, OccurredAt), OrderEventTypes.Confirmed, typeof(OrderConfirmed) },
            { new OrderShippedDomainEvent(orderId, OccurredAt), OrderEventTypes.Shipped, typeof(OrderShipped) },
            { new OrderCancelledDomainEvent(orderId, OccurredAt), OrderEventTypes.Cancelled, typeof(OrderCancelled) }
        };
    }

    [Theory]
    [MemberData(nameof(StatusEvents))]
    public void Create_ForStatusChange_MapsToContract(IDomainEvent domainEvent, string expectedType, Type contractType)
    {
        var message = OutboxMessageFactory.Create(domainEvent, null);

        Assert.Equal(expectedType, message.Type);
        Assert.Null(message.TraceParent);

        var contract = (IOrderEvent)JsonSerializer.Deserialize(message.Payload, contractType, EventSerializer.Options)!;
        Assert.Equal(message.Id, contract.EventId);
        Assert.Equal(message.AggregateId, contract.OrderId);
        Assert.Equal(OccurredAt, contract.OccurredAt);
    }

    [Fact]
    public void Create_UsesCamelCasePayload()
    {
        var message = OutboxMessageFactory.Create(new OrderConfirmedDomainEvent(Guid.NewGuid(), OccurredAt), null);

        using var document = JsonDocument.Parse(message.Payload);
        Assert.True(document.RootElement.TryGetProperty("eventId", out _));
        Assert.True(document.RootElement.TryGetProperty("orderId", out _));
    }

    [Fact]
    public void Create_GeneratesTimeOrderedEventIds()
    {
        var message = OutboxMessageFactory.Create(new OrderConfirmedDomainEvent(Guid.NewGuid(), OccurredAt), null);

        Assert.Equal(7, message.Id.Version);
    }

    [Fact]
    public void Create_ForUnknownEvent_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => OutboxMessageFactory.Create(new UnknownDomainEvent(OccurredAt), null));
    }

    private sealed record UnknownDomainEvent(DateTimeOffset OccurredAt) : IDomainEvent;
}
