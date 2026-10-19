using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrderFlow.Consumer.Warehouse;

namespace OrderFlow.Consumer.IntegrationTests.Infrastructure;

public sealed class FakeWarehouse : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<Guid, Func<int, HttpStatusCode>> _behaviors = new();
    private readonly ConcurrentDictionary<Guid, int> _attempts = new();
    private readonly ConcurrentQueue<WarehouseOrderEvent> _accepted = new();

    public void RespondFor(Guid orderId, Func<int, HttpStatusCode> behavior) => _behaviors[orderId] = behavior;

    public int AttemptsFor(Guid orderId) => _attempts.GetValueOrDefault(orderId);

    public IReadOnlyList<WarehouseOrderEvent> AcceptedFor(Guid orderId) =>
        [.. _accepted.Where(orderEvent => orderEvent.OrderId == orderId)];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var orderEvent = (await request.Content!.ReadFromJsonAsync<WarehouseOrderEvent>(JsonOptions, cancellationToken))!;
        var attempt = _attempts.AddOrUpdate(orderEvent.OrderId, 1, (_, current) => current + 1);

        var statusCode = _behaviors.TryGetValue(orderEvent.OrderId, out var behavior)
            ? behavior(attempt)
            : HttpStatusCode.Accepted;

        if (statusCode == HttpStatusCode.Accepted)
        {
            _accepted.Enqueue(orderEvent);
        }

        return new HttpResponseMessage(statusCode) { Content = new StringContent($"status {(int)statusCode}") };
    }

    protected override void Dispose(bool disposing)
    {
    }
}
