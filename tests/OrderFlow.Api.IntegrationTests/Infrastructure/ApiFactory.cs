using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts;
using OrderFlow.Infrastructure.Persistence;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(OrderFlow.Api.IntegrationTests.Infrastructure.ApiFactory))]

namespace OrderFlow.Api.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("orderflow")
        .Build();

    private readonly KafkaContainer _kafka = new KafkaBuilder("apache/kafka:4.3.1")
        .Build();

    public string KafkaBootstrapServers => _kafka.GetBootstrapAddress();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _kafka.StartAsync());

        await CreateTopicAsync();
        await ApplyMigrationsAsync();

        StartServer();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _kafka.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public OrderFlowDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<OrderFlowDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:OrderFlow", _postgres.GetConnectionString());
        builder.UseSetting("Kafka:BootstrapServers", KafkaBootstrapServers);
        builder.UseSetting("Outbox:PollingInterval", "00:00:00.200");
    }

    private async Task CreateTopicAsync()
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = KafkaBootstrapServers }).Build();

        await admin.CreateTopicsAsync(
        [
            new TopicSpecification { Name = Topics.Orders, NumPartitions = 3, ReplicationFactor = 1 }
        ]);
    }

    private async Task ApplyMigrationsAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }
}
