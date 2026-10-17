using OrderFlow.Infrastructure.Outbox;

namespace OrderFlow.Infrastructure.Tests.Outbox;

public sealed class OutboxMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [Fact]
    public void MarkFailed_IncrementsAttemptsAndKeepsMessagePending()
    {
        var message = CreateMessage();

        message.MarkFailed("timeout");
        message.MarkFailed("timeout again");

        Assert.Equal(2, message.Attempts);
        Assert.Equal("timeout again", message.LastError);
        Assert.Null(message.ProcessedAt);
        Assert.Null(message.FailedAt);
    }

    [Fact]
    public void MarkFailed_TruncatesLongErrors()
    {
        var message = CreateMessage();

        message.MarkFailed(new string('x', OutboxMessage.MaxErrorLength + 100));

        Assert.Equal(OutboxMessage.MaxErrorLength, message.LastError!.Length);
    }

    [Fact]
    public void MarkFailedPermanently_SetsFailedAt()
    {
        var message = CreateMessage();

        message.MarkFailedPermanently("too large", Now);

        Assert.Equal(Now, message.FailedAt);
        Assert.Equal(1, message.Attempts);
        Assert.Equal("too large", message.LastError);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public void MarkProcessed_ClearsLastError()
    {
        var message = CreateMessage();
        message.MarkFailed("timeout");

        message.MarkProcessed(Now);

        Assert.Equal(Now, message.ProcessedAt);
        Assert.Null(message.LastError);
        Assert.Equal(1, message.Attempts);
    }

    private static OutboxMessage CreateMessage() =>
        OutboxMessage.Create(Guid.CreateVersion7(), Guid.NewGuid(), "order.created.v1", "{}", null, Now);
}
