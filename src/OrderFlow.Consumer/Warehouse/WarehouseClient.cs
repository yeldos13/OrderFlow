namespace OrderFlow.Consumer.Warehouse;

public sealed class WarehouseClient(HttpClient httpClient)
{
    public async Task SendAsync(WarehouseOrderEvent orderEvent, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("api/order-events", orderEvent, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
