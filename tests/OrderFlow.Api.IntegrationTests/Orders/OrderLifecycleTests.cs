using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.IntegrationTests.Infrastructure;
using OrderFlow.Contracts;
using OrderFlow.Contracts.Orders;

namespace OrderFlow.Api.IntegrationTests.Orders;

public sealed class OrderLifecycleTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ConfirmAndShip_PublishEventsInOrderOnOnePartition()
    {
        var order = await OrdersApi.CreateOrderAsync(_client, _cancellationToken);

        using var confirm = await _client.PostAsync($"/api/orders/{order.Id}/confirm", null, _cancellationToken);
        using var ship = await _client.PostAsync($"/api/orders/{order.Id}/ship", null, _cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ship.StatusCode);

        var stored = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}", OrdersApi.JsonOptions, _cancellationToken);
        Assert.Equal("Shipped", stored!.Status);

        var records = await KafkaTopicReader.ReadByKeyAsync(
            factory.KafkaBootstrapServers, Topics.Orders, order.Id.ToString(), 3, TimeSpan.FromSeconds(20));

        Assert.Equal(
            [OrderEventTypes.Created, OrderEventTypes.Confirmed, OrderEventTypes.Shipped],
            records.Select(record => record.Headers[MessageHeaders.EventType]));
        Assert.Single(records.Select(record => record.Partition).Distinct());
    }

    [Fact]
    public async Task CancelShippedOrder_Returns409()
    {
        var order = await OrdersApi.CreateOrderAsync(_client, _cancellationToken);
        (await _client.PostAsync($"/api/orders/{order.Id}/confirm", null, _cancellationToken)).Dispose();
        (await _client.PostAsync($"/api/orders/{order.Id}/ship", null, _cancellationToken)).Dispose();

        using var response = await _client.PostAsync($"/api/orders/{order.Id}/cancel", null, _cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_cancellationToken);
        Assert.Equal("Invalid order state", problem!.Title);
    }

    [Fact]
    public async Task ConcurrentStatusChanges_OnlyOneSucceeds()
    {
        var order = await OrdersApi.CreateOrderAsync(_client, _cancellationToken);

        var responses = await Task.WhenAll(
            _client.PostAsync($"/api/orders/{order.Id}/confirm", null, _cancellationToken),
            _client.PostAsync($"/api/orders/{order.Id}/cancel", null, _cancellationToken));

        Assert.Equal(
            [HttpStatusCode.NoContent, HttpStatusCode.Conflict],
            responses.Select(response => response.StatusCode).Order());

        var stored = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}", OrdersApi.JsonOptions, _cancellationToken);
        Assert.Contains(stored!.Status, new[] { "Confirmed", "Cancelled" });

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task UnknownOrder_Returns404()
    {
        using var get = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}", _cancellationToken);
        using var cancel = await _client.PostAsync($"/api/orders/{Guid.NewGuid()}/cancel", null, _cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByCustomerAndStatusWithPaging()
    {
        var customerId = Guid.NewGuid();
        var first = await OrdersApi.CreateOrderAsync(_client, _cancellationToken, customerId);
        var second = await OrdersApi.CreateOrderAsync(_client, _cancellationToken, customerId);
        await OrdersApi.CreateOrderAsync(_client, _cancellationToken, customerId);
        (await _client.PostAsync($"/api/orders/{first.Id}/cancel", null, _cancellationToken)).Dispose();

        var all = await _client.GetFromJsonAsync<PageDto<OrderSummaryDto>>(
            $"/api/orders?customerId={customerId}&pageSize=2", OrdersApi.JsonOptions, _cancellationToken);
        var cancelled = await _client.GetFromJsonAsync<PageDto<OrderSummaryDto>>(
            $"/api/orders?customerId={customerId}&status=Cancelled", OrdersApi.JsonOptions, _cancellationToken);

        Assert.Equal(3, all!.TotalCount);
        Assert.Equal(2, all.TotalPages);
        Assert.Equal(2, all.Items.Count);
        Assert.DoesNotContain(all.Items, item => item.Id == first.Id);
        Assert.Contains(all.Items, item => item.Id == second.Id);
        Assert.Equal(first.Id, Assert.Single(cancelled!.Items).Id);
    }

    [Fact]
    public async Task ReadinessCheck_IsHealthy()
    {
        using var response = await _client.GetAsync("/health/ready", _cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
