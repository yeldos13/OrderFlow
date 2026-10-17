using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderFlow.Consumer.Deduplication;
using OrderFlow.Consumer.Processing;
using OrderFlow.Consumer.Warehouse;
using OrderFlow.Contracts;
using OrderFlow.Contracts.Orders;

namespace OrderFlow.Consumer.Tests.Processing;

public sealed class OrderEventHandlerTests
{
    private readonly Mock<IWarehouseClient> _warehouse = new(MockBehavior.Strict);
    private readonly Mock<IProcessedEventStore> _processedEvents = new(MockBehavior.Strict);
    private readonly OrderEventHandler _handler;

    public OrderEventHandlerTests()
    {
        _handler = new OrderEventHandler(_warehouse.Object, _processedEvents.Object, NullLogger<OrderEventHandler>.Instance);
    }

    [Fact]
    public async Task NewEvent_IsSentToWarehouseAndMarkedProcessed()
    {
        var orderEvent = new OrderConfirmed(Guid.CreateVersion7(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var calls = new List<string>();
        WarehouseOrderEvent? sent = null;

        _processedEvents
            .Setup(store => store.IsProcessedAsync(orderEvent.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _warehouse
            .Setup(client => client.SendAsync(It.IsAny<WarehouseOrderEvent>(), It.IsAny<CancellationToken>()))
            .Callback<WarehouseOrderEvent, CancellationToken>((request, _) =>
            {
                sent = request;
                calls.Add("send");
            })
            .Returns(Task.CompletedTask);
        _processedEvents
            .Setup(store => store.MarkProcessedAsync(orderEvent.EventId, OrderEventTypes.Confirmed, orderEvent.OrderId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("mark"))
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(Message(OrderEventTypes.Confirmed, orderEvent), TestContext.Current.CancellationToken);

        Assert.Equal(["send", "mark"], calls);
        Assert.NotNull(sent);
        Assert.Equal(orderEvent.EventId, sent.EventId);
        Assert.Equal(orderEvent.OrderId, sent.OrderId);
        Assert.Equal(OrderEventTypes.Confirmed, sent.EventType);
        Assert.Equal(orderEvent.OccurredAt, sent.OccurredAt);
        Assert.Equal(orderEvent.OrderId, sent.Data.GetProperty("orderId").GetGuid());
    }

    [Fact]
    public async Task AlreadyProcessedEvent_IsSkipped()
    {
        var orderEvent = new OrderShipped(Guid.CreateVersion7(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        _processedEvents
            .Setup(store => store.IsProcessedAsync(orderEvent.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _handler.HandleAsync(Message(OrderEventTypes.Shipped, orderEvent), TestContext.Current.CancellationToken);

        _warehouse.VerifyNoOtherCalls();
        _processedEvents.Verify(store => store.MarkProcessedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WarehouseFailure_DoesNotMarkEventProcessed()
    {
        var orderEvent = new OrderCancelled(Guid.CreateVersion7(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        _processedEvents
            .Setup(store => store.IsProcessedAsync(orderEvent.EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _warehouse
            .Setup(client => client.SendAsync(It.IsAny<WarehouseOrderEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("503"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _handler.HandleAsync(Message(OrderEventTypes.Cancelled, orderEvent), TestContext.Current.CancellationToken));

        _processedEvents.Verify(store => store.MarkProcessedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("order.unknown.v1")]
    public async Task UnknownEventType_IsUnsupported(string? eventType)
    {
        var message = new ReceivedMessage(null, "{}", null, eventType, null);

        await Assert.ThrowsAsync<UnsupportedMessageException>(() =>
            _handler.HandleAsync(message, TestContext.Current.CancellationToken));

        _warehouse.VerifyNoOtherCalls();
        _processedEvents.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"orderId\": 42}")]
    public async Task MalformedBody_IsUnsupported(string body)
    {
        var message = new ReceivedMessage(null, body, null, OrderEventTypes.Confirmed, null);

        await Assert.ThrowsAsync<UnsupportedMessageException>(() =>
            _handler.HandleAsync(message, TestContext.Current.CancellationToken));

        _warehouse.VerifyNoOtherCalls();
        _processedEvents.VerifyNoOtherCalls();
    }

    private static ReceivedMessage Message(string eventType, IOrderEvent orderEvent) => new(
        orderEvent.OrderId.ToString(),
        JsonSerializer.Serialize(orderEvent, orderEvent.GetType(), EventSerializer.Options),
        orderEvent.EventId.ToString(),
        eventType,
        null);
}
