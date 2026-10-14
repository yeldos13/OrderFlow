using System.Net;

namespace OrderFlow.Consumer.Warehouse;

public sealed class WarehouseRejectedException(HttpStatusCode statusCode, string? details)
    : Exception($"Warehouse rejected the event with status {(int)statusCode}. {details}".TrimEnd())
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
