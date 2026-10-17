using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using OrderFlow.Consumer.Health;

namespace OrderFlow.Consumer.Tests.Health;

public sealed class ConsumerHealthCheckTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly ConsumerStatus _status;
    private readonly ConsumerHealthCheck _healthCheck;

    public ConsumerHealthCheckTests()
    {
        _status = new ConsumerStatus(_time);
        _healthCheck = new ConsumerHealthCheck(
            _status,
            Options.Create(new ConsumerHealthOptions
            {
                StallThreshold = TimeSpan.FromMinutes(1),
                DeadLetterWindow = TimeSpan.FromMinutes(15)
            }),
            _time);
    }

    [Fact]
    public async Task NoPartitions_IsDegraded()
    {
        var result = await CheckAsync();

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("No partitions assigned.", result.Description);
    }

    [Fact]
    public async Task ConsumingWithoutLag_IsHealthy()
    {
        _status.PartitionsAssigned([0, 1]);
        _status.MessageCompleted(0, 0);

        var result = await CheckAsync();

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(0L, result.Data["lag"]);
    }

    [Fact]
    public async Task LagWithRecentProgress_IsHealthy()
    {
        _status.PartitionsAssigned([0]);
        _status.MessageCompleted(0, 10);
        _time.Advance(TimeSpan.FromSeconds(30));

        var result = await CheckAsync();

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task LagWithoutProgress_IsStalled()
    {
        _status.PartitionsAssigned([0, 1]);
        _status.MessageCompleted(0, 0);
        _status.MessageReceived(1, 3);
        _time.Advance(TimeSpan.FromMinutes(2));

        var result = await CheckAsync();

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.StartsWith("Consumer is stalled: lag 3", result.Description);
    }

    [Fact]
    public async Task NoLagAndNoTraffic_IsHealthy()
    {
        _status.PartitionsAssigned([0]);
        _time.Advance(TimeSpan.FromHours(1));

        var result = await CheckAsync();

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task RecentDeadLetter_IsDegradedUntilWindowPasses()
    {
        _status.PartitionsAssigned([0]);
        _status.MessageDeadLettered(0, 0);

        var recent = await CheckAsync();
        _time.Advance(TimeSpan.FromMinutes(16));
        var later = await CheckAsync();

        Assert.Equal(HealthStatus.Degraded, recent.Status);
        Assert.Equal(1L, recent.Data["deadLettered"]);
        Assert.Equal(HealthStatus.Healthy, later.Status);
        Assert.Equal(1L, later.Data["deadLettered"]);
    }

    [Fact]
    public async Task RevokedPartitions_AreRemoved()
    {
        _status.PartitionsAssigned([0, 1, 2]);
        _status.MessageReceived(2, 5);
        _status.PartitionsRevoked([1, 2]);

        var result = await CheckAsync();

        Assert.Equal(new[] { 0 }, (IReadOnlyList<int>)result.Data["assignedPartitions"]);
        Assert.Equal(0L, result.Data["lag"]);
    }

    private Task<HealthCheckResult> CheckAsync() =>
        _healthCheck.CheckHealthAsync(
            new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("consumer", _healthCheck, HealthStatus.Degraded, null)
            },
            TestContext.Current.CancellationToken);
}
