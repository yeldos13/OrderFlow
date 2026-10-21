using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.IntegrationTests.Infrastructure;
using OrderFlow.Contracts;
using OrderFlow.Contracts.Orders;

namespace OrderFlow.Api.IntegrationTests.Orders;

public sealed class CreateOrderTests(ApiFactory factory)
{
    private static readonly TimeSpan KafkaTimeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient _client = factory.CreateClient();
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateOrder_PersistsOrderAndPublishesEvent()
    {
        var customerId = Guid.NewGuid();

        using var response = await OrdersApi.CreateAsync(_client, OrdersApi.NewOrder(customerId), Guid.NewGuid().ToString(), _cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderDto>(OrdersApi.JsonOptions, _cancellationToken))!;
        Assert.Equal($"/api/orders/{order.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal("Created", order.Status);
        Assert.Equal(21m, order.TotalAmount);

        var stored = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}", OrdersApi.JsonOptions, _cancellationToken);
        Assert.Equal(order.Id, stored!.Id);
        Assert.Equal(order.CreatedAt, stored.CreatedAt);
        Assert.Single(stored.Items);

        var record = Assert.Single(await KafkaTopicReader.ReadByKeyAsync(
            factory.KafkaBootstrapServers, Topics.Orders, order.Id.ToString(), 1, KafkaTimeout));
        Assert.Equal(OrderEventTypes.Created, record.Headers[MessageHeaders.EventType]);
        Assert.True(record.Headers.ContainsKey(MessageHeaders.TraceParent));

        var published = JsonSerializer.Deserialize<OrderCreated>(record.Value, EventSerializer.Options)!;
        Assert.Equal(order.Id, published.OrderId);
        Assert.Equal(customerId, published.CustomerId);
        Assert.Equal(21m, published.TotalAmount);
        Assert.Equal(published.EventId.ToString(), record.Headers[MessageHeaders.EventId]);

        await using var dbContext = factory.CreateDbContext();
        var outbox = await dbContext.OutboxMessages.SingleAsync(message => message.AggregateId == order.Id, _cancellationToken);
        Assert.NotNull(outbox.ProcessedAt);
    }

    [Fact]
    public async Task RepeatedRequestWithSameKey_ReturnsStoredResponse()
    {
        var customerId = Guid.NewGuid();
        var body = OrdersApi.NewOrder(customerId);
        var key = Guid.NewGuid().ToString();

        using var first = await OrdersApi.CreateAsync(_client, body, key, _cancellationToken);
        using var second = await OrdersApi.CreateAsync(_client, body, key, _cancellationToken);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(first.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal("true", Assert.Single(second.Headers.GetValues("Idempotent-Replayed")));
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Equal(
            await first.Content.ReadAsStringAsync(_cancellationToken),
            await second.Content.ReadAsStringAsync(_cancellationToken));

        var page = await _client.GetFromJsonAsync<PageDto<OrderSummaryDto>>(
            $"/api/orders?customerId={customerId}", OrdersApi.JsonOptions, _cancellationToken);
        Assert.Equal(1, page!.TotalCount);
    }

    [Fact]
    public async Task ConcurrentRequestsWithSameKey_CreateSingleOrder()
    {
        var customerId = Guid.NewGuid();
        var body = OrdersApi.NewOrder(customerId);
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => OrdersApi.CreateAsync(_client, body, key, _cancellationToken)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Single(responses.Select(response => response.Headers.Location).Distinct());

        var page = await _client.GetFromJsonAsync<PageDto<OrderSummaryDto>>(
            $"/api/orders?customerId={customerId}", OrdersApi.JsonOptions, _cancellationToken);
        Assert.Equal(1, page!.TotalCount);

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task SameKeyWithDifferentBody_Returns422()
    {
        var key = Guid.NewGuid().ToString();

        using var first = await OrdersApi.CreateAsync(_client, OrdersApi.NewOrder(), key, _cancellationToken);
        using var second = await OrdersApi.CreateAsync(_client, OrdersApi.NewOrder(), key, _cancellationToken);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task MissingIdempotencyKey_Returns400()
    {
        using var response = await OrdersApi.CreateAsync(_client, OrdersApi.NewOrder(), null, _cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_cancellationToken);
        Assert.Equal("Invalid idempotency key", problem!.Title);
    }

    [Fact]
    public async Task InvalidRequest_ReturnsValidationProblem()
    {
        var body = new
        {
            customerId = Guid.Empty,
            items = new[] { new { productId = Guid.NewGuid(), quantity = 0, unitPrice = 1m } }
        };

        using var response = await OrdersApi.CreateAsync(_client, body, Guid.NewGuid().ToString(), _cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(_cancellationToken);
        Assert.Contains("customerId", problem!.Errors.Keys);
        Assert.Contains("items[0].quantity", problem.Errors.Keys);
    }

    [Fact]
    public async Task FailedRequest_DoesNotConsumeIdempotencyKey()
    {
        var key = Guid.NewGuid().ToString();
        var invalid = new { customerId = Guid.NewGuid(), items = Array.Empty<object>() };

        using var failed = await OrdersApi.CreateAsync(_client, invalid, key, _cancellationToken);
        using var retried = await OrdersApi.CreateAsync(_client, OrdersApi.NewOrder(), key, _cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
    }
}
