using System.Net;

namespace OrderFlow.Consumer.Warehouse;

public sealed class WarehouseClient(HttpClient httpClient) : IWarehouseClient
{
    private const int MaxDetailsLength = 500;

    public async Task SendAsync(WarehouseOrderEvent orderEvent, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("api/order-events", orderEvent, cancellationToken);

        if (IsRejection(response.StatusCode))
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new WarehouseRejectedException(
                response.StatusCode,
                details.Length > MaxDetailsLength ? details[..MaxDetailsLength] : details);
        }

        response.EnsureSuccessStatusCode();
    }

    private static bool IsRejection(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity;
}
