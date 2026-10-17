using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using OrderFlow.Consumer.Warehouse;

namespace OrderFlow.Consumer.Tests.Warehouse;

public sealed class WarehouseClientTests
{
    private readonly Mock<HttpMessageHandler> _handler = new();

    [Fact]
    public async Task SuccessfulResponse_PostsEvent()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        Respond(HttpStatusCode.Accepted, onRequest: async request =>
        {
            captured = request;
            body = await request.Content!.ReadAsStringAsync();
        });
        var orderEvent = CreateEvent();

        await CreateClient().SendAsync(orderEvent, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal("http://warehouse.test/api/order-events", captured.RequestUri!.ToString());

        using var document = JsonDocument.Parse(body!);
        Assert.Equal(orderEvent.EventId, document.RootElement.GetProperty("eventId").GetGuid());
        Assert.Equal(orderEvent.EventType, document.RootElement.GetProperty("eventType").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task ClientError_IsRejection(HttpStatusCode statusCode)
    {
        Respond(statusCode, "invalid event");

        var exception = await Assert.ThrowsAsync<WarehouseRejectedException>(() =>
            CreateClient().SendAsync(CreateEvent(), TestContext.Current.CancellationToken));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Contains("invalid event", exception.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task TransientError_IsNotRejection(HttpStatusCode statusCode)
    {
        Respond(statusCode);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateClient().SendAsync(CreateEvent(), TestContext.Current.CancellationToken));

        Assert.Equal(statusCode, exception.StatusCode);
    }

    [Fact]
    public async Task Rejection_TruncatesLongDetails()
    {
        Respond(HttpStatusCode.BadRequest, new string('x', 5000));

        var exception = await Assert.ThrowsAsync<WarehouseRejectedException>(() =>
            CreateClient().SendAsync(CreateEvent(), TestContext.Current.CancellationToken));

        Assert.True(exception.Message.Length < 600);
    }

    private WarehouseClient CreateClient() =>
        new(new HttpClient(_handler.Object) { BaseAddress = new Uri("http://warehouse.test/") });

    private void Respond(HttpStatusCode statusCode, string content = "", Func<HttpRequestMessage, Task>? onRequest = null)
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, _) =>
            {
                if (onRequest is not null)
                {
                    await onRequest(request);
                }

                return new HttpResponseMessage(statusCode) { Content = new StringContent(content) };
            });
    }

    private static WarehouseOrderEvent CreateEvent() => new(
        Guid.CreateVersion7(),
        "order.confirmed.v1",
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        JsonDocument.Parse("{}").RootElement);
}
