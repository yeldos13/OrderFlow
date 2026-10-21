using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderFlow.Api.IntegrationTests.Infrastructure;
using OrderFlow.Contracts;
using OrderFlow.Infrastructure.Messaging;
using OrderFlow.Infrastructure.Outbox;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Api.IntegrationTests.Outbox;

public sealed class OutboxProcessorTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Database = "outbox_processor_tests";

    private readonly FakePublisher _publisher = new();
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        _services = new ServiceCollection()
            .AddDbContext<OrderFlowDbContext>(options => options
                .UseNpgsql(factory.ConnectionStringFor(Database))
                .UseSnakeCaseNamingConvention())
            .BuildServiceProvider();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync(_cancellationToken);
        await dbContext.OutboxMessages.ExecuteDeleteAsync(_cancellationToken);
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task PublishesPendingMessagesInOrder()
    {
        var orderId = Guid.NewGuid();
        var first = await AddMessageAsync(minutesAgo: 2, orderId);
        var second = await AddMessageAsync(minutesAgo: 1, orderId);

        var handled = await CreateProcessor().ProcessBatchAsync(_cancellationToken);

        Assert.Equal(2, handled);
        Assert.Equal([first.Id.ToString(), second.Id.ToString()], _publisher.Published.Select(message => message.Headers[MessageHeaders.EventId]));
        Assert.All(await LoadAsync(first.Id, second.Id), message => Assert.NotNull(message.ProcessedAt));
    }

    [Fact]
    public async Task TransientFailure_HoldsLaterMessagesOfSameOrder()
    {
        var orderId = Guid.NewGuid();
        var first = await AddMessageAsync(minutesAgo: 2, orderId);
        var second = await AddMessageAsync(minutesAgo: 1, orderId);
        _publisher.FailWith(first.Id, new TimeoutException("broker timeout"));

        var handled = await CreateProcessor().ProcessBatchAsync(_cancellationToken);

        Assert.Equal(0, handled);
        Assert.Empty(_publisher.Published);

        var stored = await LoadAsync(first.Id, second.Id);
        Assert.Equal(1, stored[0].Attempts);
        Assert.Equal("broker timeout", stored[0].LastError);
        Assert.All(stored, message => Assert.Null(message.ProcessedAt));
        Assert.All(stored, message => Assert.Null(message.FailedAt));
    }

    [Fact]
    public async Task TransientFailure_DoesNotBlockOtherOrders()
    {
        var failing = await AddMessageAsync(minutesAgo: 2);
        var other = await AddMessageAsync(minutesAgo: 1);
        _publisher.FailWith(failing.Id, new TimeoutException("broker timeout"));

        var handled = await CreateProcessor().ProcessBatchAsync(_cancellationToken);

        Assert.Equal(1, handled);
        Assert.Equal(other.Id.ToString(), Assert.Single(_publisher.Published).Headers[MessageHeaders.EventId]);

        var stored = await LoadAsync(failing.Id, other.Id);
        Assert.Null(stored[0].ProcessedAt);
        Assert.Equal(1, stored[0].Attempts);
        Assert.NotNull(stored[1].ProcessedAt);
    }

    [Fact]
    public async Task PermanentFailure_SkipsMessageAndContinues()
    {
        var orderId = Guid.NewGuid();
        var first = await AddMessageAsync(minutesAgo: 2, orderId);
        var second = await AddMessageAsync(minutesAgo: 1, orderId);
        _publisher.FailWith(first.Id, new PermanentPublishException("too large", new Exception()));

        var handled = await CreateProcessor().ProcessBatchAsync(_cancellationToken);

        Assert.Equal(2, handled);
        Assert.Equal(second.Id.ToString(), Assert.Single(_publisher.Published).Headers[MessageHeaders.EventId]);

        var stored = await LoadAsync(first.Id, second.Id);
        Assert.NotNull(stored[0].FailedAt);
        Assert.Null(stored[0].ProcessedAt);
        Assert.NotNull(stored[1].ProcessedAt);
        Assert.Equal(0, await CreateProcessor().ProcessBatchAsync(_cancellationToken));
    }

    [Fact]
    public async Task AnotherInstanceHoldingTheLock_IsNotOvertaken()
    {
        await AddMessageAsync(minutesAgo: 1);

        await using var otherInstance = CreateDbContext();
        await using var transaction = await otherInstance.Database.BeginTransactionAsync(_cancellationToken);
        await otherInstance.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({OutboxProcessor.ProcessorLockKey})", _cancellationToken);

        var handled = await CreateProcessor().ProcessBatchAsync(_cancellationToken);

        Assert.Equal(0, handled);
        Assert.Empty(_publisher.Published);
    }

    private OutboxProcessor CreateProcessor() => new(
        _services.GetRequiredService<IServiceScopeFactory>(),
        _publisher,
        Options.Create(new OutboxOptions { BatchSize = 10 }),
        TimeProvider.System,
        NullLogger<OutboxProcessor>.Instance);

    private OrderFlowDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<OrderFlowDbContext>()
            .UseNpgsql(factory.ConnectionStringFor(Database))
            .UseSnakeCaseNamingConvention()
            .Options);

    private async Task<OutboxMessage> AddMessageAsync(int minutesAgo, Guid? orderId = null)
    {
        var message = OutboxMessage.Create(
            Guid.CreateVersion7(),
            orderId ?? Guid.NewGuid(),
            "order.confirmed.v1",
            "{}",
            null,
            DateTimeOffset.UtcNow.AddMinutes(-minutesAgo));

        await using var dbContext = CreateDbContext();
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(_cancellationToken);
        return message;
    }

    private async Task<List<OutboxMessage>> LoadAsync(params Guid[] ids)
    {
        await using var dbContext = CreateDbContext();
        var messages = await dbContext.OutboxMessages.AsNoTracking()
            .Where(message => ids.Contains(message.Id))
            .ToListAsync(_cancellationToken);
        return [.. ids.Select(id => messages.Single(message => message.Id == id))];
    }

    private sealed class FakePublisher : IMessagePublisher
    {
        private readonly Dictionary<string, Exception> _failures = [];

        private readonly Lock _lock = new();

        public List<OutgoingMessage> Published { get; } = [];

        public void FailWith(Guid messageId, Exception exception) => _failures[messageId.ToString()] = exception;

        public Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
        {
            if (_failures.TryGetValue(message.Headers[MessageHeaders.EventId]!, out var exception))
            {
                throw exception;
            }

            lock (_lock)
            {
                Published.Add(message);
            }

            return Task.CompletedTask;
        }
    }
}
