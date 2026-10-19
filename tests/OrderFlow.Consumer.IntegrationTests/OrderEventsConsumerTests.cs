using System.Net;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Consumer.IntegrationTests.Infrastructure;
using OrderFlow.Contracts;
using OrderFlow.Contracts.Orders;

namespace OrderFlow.Consumer.IntegrationTests;

public sealed class OrderEventsConsumerTests(ConsumerFactory factory)
{
    private static readonly TimeSpan KafkaTimeout = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task ValidEvent_IsDeliveredAndRecorded()
    {
        var orderEvent = NewConfirmed();

        await KafkaTestClient.PublishEventAsync(factory.KafkaBootstrapServers, orderEvent);

        var accepted = await Eventually.GetAsync(() => factory.Warehouse.AcceptedFor(orderEvent.OrderId), events => events.Count > 0);
        var delivered = Assert.Single(accepted);
        Assert.Equal(orderEvent.EventId, delivered.EventId);
        Assert.Equal(OrderEventTypes.Confirmed, delivered.EventType);
        Assert.Equal(orderEvent.OccurredAt, delivered.OccurredAt);

        Assert.True(await IsRecordedAsync(orderEvent.EventId));
    }

    [Fact]
    public async Task RedeliveredEvent_IsSentToWarehouseOnce()
    {
        var duplicate = NewConfirmed();
        var marker = new OrderShipped(Guid.CreateVersion7(), duplicate.OrderId, DateTimeOffset.UtcNow);

        await KafkaTestClient.PublishEventAsync(factory.KafkaBootstrapServers, duplicate);
        await KafkaTestClient.PublishEventAsync(factory.KafkaBootstrapServers, duplicate);
        await KafkaTestClient.PublishEventAsync(factory.KafkaBootstrapServers, marker);

        var accepted = await Eventually.GetAsync(
            () => factory.Warehouse.AcceptedFor(duplicate.OrderId),
            events => events.Any(orderEvent => orderEvent.EventId == marker.EventId));

        Assert.Equal([duplicate.EventId, marker.EventId], accepted.Select(orderEvent => orderEvent.EventId));
    }

    [Fact]
    public async Task UnknownMessage_IsMovedToDeadLetterAndConsumptionContinues()
    {
        var orderId = Guid.NewGuid();
        var next = new OrderCancelled(Guid.CreateVersion7(), orderId, DateTimeOffset.UtcNow);

        await KafkaTestClient.PublishAsync(factory.KafkaBootstrapServers, orderId.ToString(), "garbage");
        await KafkaTestClient.PublishEventAsync(factory.KafkaBootstrapServers, next);

        var deadLetter = Assert.Single(await KafkaTestClient.ReadByKeyAsync(
            factory.KafkaBootstrapServers, Topics.OrdersDeadLetter, orderId.ToString(), 1, KafkaTimeout));
        Assert.Equal("garbage", deadLetter.Value);
        Assert.Equal("Unknown event type ''.", deadLetter.Headers[DeadLetterHeaders.Reason]);
        Assert.EndsWith("UnsupportedMessageException", deadLetter.Headers[DeadLetterHeaders.ExceptionType]);
        Assert.Equal(Topics.Orders, deadLetter.Headers[DeadLetterHeaders.OriginalTopic]);
        Assert.True(deadLetter.Headers.ContainsKey(DeadLetterHeaders.OriginalPartition));
        Assert.True(deadLetter.Headers.ContainsKey(DeadLetterHeaders.OriginalOffset));
        Assert.Equal("integration-tests", deadLetter.Headers[DeadLetterHeaders.ConsumerGroup]);

        var accepted = await Eventually.GetAsync(() => factory.Warehouse.AcceptedFor(orderId), events => events.Count > 0);
        Assert.Equal(next.EventId, Assert.Single(accepted).EventId);
    }

    [Fact]
    public async Task RejectedEvent_IsMovedToDeadLetterWithOriginalHeaders()
    {
        var orderEvent = NewConfirmed();
        factory.Warehouse.RespondFor(orderEvent.OrderId, _ => HttpStatusCode.BadRequest);

        await KafkaTestClient.PublishEventAsync(factory.KafkaBootstrapServers, orderEvent);

        var deadLetter = Assert.Single(await KafkaTestClient.ReadByKeyAsync(
            factory.KafkaBootstrapServers, Topics.OrdersDeadLetter, orderEvent.OrderId.ToString(), 1, KafkaTimeout));
        Assert.EndsWith("WarehouseRejectedException", deadLetter.Headers[DeadLetterHeaders.ExceptionType]);
        Assert.Contains("400", deadLetter.Headers[DeadLetterHeaders.Reason]);
        Assert.Equal(OrderEventTypes.Confirmed, deadLetter.Headers[MessageHeaders.EventType]);
        Assert.Equal(orderEvent.EventId.ToString(), deadLetter.Headers[MessageHeaders.EventId]);

        Assert.Equal(1, factory.Warehouse.AttemptsFor(orderEvent.OrderId));
        Assert.False(await IsRecordedAsync(orderEvent.EventId));
    }

    [Fact]
    public async Task TransientFailures_AreRetriedUntilDelivered()
    {
        var orderEvent = NewConfirmed();
        factory.Warehouse.RespondFor(
            orderEvent.OrderId,
            attempt => attempt <= 4 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Accepted);

        await KafkaTestClient.PublishEventAsync(factory.KafkaBootstrapServers, orderEvent);

        var accepted = await Eventually.GetAsync(() => factory.Warehouse.AcceptedFor(orderEvent.OrderId), events => events.Count > 0);

        Assert.Equal(orderEvent.EventId, Assert.Single(accepted).EventId);
        Assert.Equal(5, factory.Warehouse.AttemptsFor(orderEvent.OrderId));
        Assert.True(await IsRecordedAsync(orderEvent.EventId));
        Assert.Empty(await KafkaTestClient.ReadByKeyAsync(
            factory.KafkaBootstrapServers, Topics.OrdersDeadLetter, orderEvent.OrderId.ToString(), 1, TimeSpan.FromSeconds(3)));
    }

    private static OrderConfirmed NewConfirmed() =>
        new(Guid.CreateVersion7(), Guid.NewGuid(), new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));

    private async Task<bool> IsRecordedAsync(Guid eventId)
    {
        await using var dbContext = factory.CreateDbContext();
        return await Eventually.GetAsync(
            () => dbContext.ProcessedEvents.AnyAsync(processedEvent => processedEvent.EventId == eventId),
            recorded => recorded,
            TimeSpan.FromSeconds(5));
    }
}
