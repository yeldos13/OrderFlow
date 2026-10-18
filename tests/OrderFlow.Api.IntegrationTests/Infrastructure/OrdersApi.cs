using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderFlow.Api.IntegrationTests.Infrastructure;

public sealed record OrderItemDto(Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderSummaryDto(Guid Id, Guid CustomerId, string Status, decimal TotalAmount, DateTimeOffset CreatedAt);

public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public static class OrdersApi
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static object NewOrder(Guid? customerId = null, int quantity = 2, decimal unitPrice = 10.5m) => new
    {
        customerId = customerId ?? Guid.NewGuid(),
        items = new[] { new { productId = Guid.NewGuid(), quantity, unitPrice } }
    };

    public static Task<HttpResponseMessage> CreateAsync(HttpClient client, object body, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(body) };

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return client.SendAsync(request, cancellationToken);
    }

    public static async Task<OrderDto> CreateOrderAsync(HttpClient client, CancellationToken cancellationToken, Guid? customerId = null)
    {
        using var response = await CreateAsync(client, NewOrder(customerId), Guid.NewGuid().ToString(), cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions, cancellationToken))!;
    }
}
