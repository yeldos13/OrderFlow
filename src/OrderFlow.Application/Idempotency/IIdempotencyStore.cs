namespace OrderFlow.Application.Idempotency;

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(string key, CancellationToken cancellationToken);

    Task<IIdempotencyScope?> TryBeginAsync(string key, string requestHash, CancellationToken cancellationToken);
}

public interface IIdempotencyScope : IAsyncDisposable
{
    Task CompleteAsync(int statusCode, string? contentType, string? body, string? location, CancellationToken cancellationToken);
}
