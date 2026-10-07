namespace OrderFlow.Application.Idempotency;

public sealed class IdempotencyRecord
{
    public const int MaxKeyLength = 128;

    private IdempotencyRecord()
    {
    }

    public string Key { get; private set; } = null!;

    public string RequestHash { get; private set; } = null!;

    public int ResponseStatusCode { get; private set; }

    public string? ResponseContentType { get; private set; }

    public string? ResponseBody { get; private set; }

    public string? ResponseLocation { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static IdempotencyRecord Start(string key, string requestHash, DateTimeOffset now) => new()
    {
        Key = key,
        RequestHash = requestHash,
        CreatedAt = now
    };

    public void Complete(int statusCode, string? contentType, string? body, string? location)
    {
        ResponseStatusCode = statusCode;
        ResponseContentType = contentType;
        ResponseBody = body;
        ResponseLocation = location;
    }
}
