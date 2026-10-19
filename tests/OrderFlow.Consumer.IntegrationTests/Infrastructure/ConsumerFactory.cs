using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Consumer.Persistence;
using OrderFlow.Consumer.Warehouse;
using OrderFlow.Contracts;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(OrderFlow.Consumer.IntegrationTests.Infrastructure.ConsumerFactory))]

namespace OrderFlow.Consumer.IntegrationTests.Infrastructure;

public sealed class ConsumerFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("orderflow_consumer")
        .Build();

    private readonly KafkaContainer _kafka = new KafkaBuilder("apache/kafka:4.3.1")
        .Build();

    public FakeWarehouse Warehouse { get; } = new();

    public string KafkaBootstrapServers => _kafka.GetBootstrapAddress();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _kafka.StartAsync());

        await CreateTopicsAsync();
        await ApplyMigrationsAsync();

        StartServer();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _kafka.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public ConsumerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ConsumerDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ConsumerDb", _postgres.GetConnectionString());
        builder.UseSetting("Kafka:BootstrapServers", KafkaBootstrapServers);
        builder.UseSetting("Kafka:GroupId", "integration-tests");
        builder.UseSetting("Kafka:RetryDelay", "00:00:00.200");
        builder.UseSetting("Warehouse:BaseUrl", "http://warehouse.test/");
        builder.UseSetting("Warehouse:Resilience:AttemptTimeout", "00:00:01");
        builder.UseSetting("Warehouse:Resilience:TotalTimeout", "00:00:05");
        builder.UseSetting("Warehouse:Resilience:MaxRetryAttempts", "2");
        builder.UseSetting("Warehouse:Resilience:RetryBaseDelay", "00:00:00.050");
        builder.UseSetting("Warehouse:Resilience:CircuitMinimumThroughput", "1000");

        builder.ConfigureTestServices(services => services
            .AddHttpClient<IWarehouseClient, WarehouseClient>()
            .ConfigurePrimaryHttpMessageHandler(() => Warehouse));
    }

    private async Task CreateTopicsAsync()
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = KafkaBootstrapServers }).Build();

        await admin.CreateTopicsAsync(
        [
            new TopicSpecification { Name = Topics.Orders, NumPartitions = 3, ReplicationFactor = 1 },
            new TopicSpecification { Name = Topics.OrdersDeadLetter, NumPartitions = 1, ReplicationFactor = 1 }
        ]);
    }

    private async Task ApplyMigrationsAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }
}
