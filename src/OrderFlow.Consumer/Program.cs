using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderFlow.Consumer.Deduplication;
using OrderFlow.Consumer.Health;
using OrderFlow.Consumer.Kafka;
using OrderFlow.Consumer.Persistence;
using OrderFlow.Consumer.Processing;
using OrderFlow.Consumer.Warehouse;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    const string ReadinessTag = "ready";

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services));

    builder.Services.AddOptions<KafkaConsumerOptions>()
        .BindConfiguration(KafkaConsumerOptions.SectionName)
        .Validate(options => !string.IsNullOrWhiteSpace(options.BootstrapServers), "Kafka bootstrap servers are not configured.")
        .Validate(options => !string.IsNullOrWhiteSpace(options.GroupId), "Kafka consumer group is not configured.")
        .Validate(options => !string.IsNullOrWhiteSpace(options.Topic), "Kafka topic is not configured.")
        .Validate(options => !string.IsNullOrWhiteSpace(options.DeadLetterTopic) && options.DeadLetterTopic != options.Topic, "Dead letter topic must be configured and differ from the source topic.")
        .Validate(options => options.ProducerMessageTimeoutMs > 0, "Producer message timeout must be positive.")
        .Validate(options => options.RetryDelay > TimeSpan.Zero, "Retry delay must be positive.")
        .ValidateOnStart();

    builder.Services.AddWarehouseClient();

    var connectionString = builder.Configuration.GetConnectionString("ConsumerDb")
        ?? throw new InvalidOperationException("Connection string 'ConsumerDb' is not configured.");

    builder.Services.AddDbContext<ConsumerDbContext>(options => options
        .UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention());

    builder.Services.AddOptions<DeduplicationOptions>()
        .BindConfiguration(DeduplicationOptions.SectionName)
        .Validate(options => options.Retention > TimeSpan.Zero, "Deduplication retention must be positive.")
        .Validate(options => options.CleanupInterval > TimeSpan.Zero, "Deduplication cleanup interval must be positive.")
        .ValidateOnStart();

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddScoped<IProcessedEventStore, ProcessedEventStore>();
    builder.Services.AddScoped<OrderEventHandler>();
    builder.Services.AddSingleton<DeadLetterPublisher>();
    builder.Services.AddHostedService<OrderEventsConsumer>();
    builder.Services.AddHostedService<ProcessedEventsCleanupService>();

    builder.Services.AddOptions<ConsumerHealthOptions>()
        .BindConfiguration(ConsumerHealthOptions.SectionName)
        .Validate(options => options.StallThreshold > TimeSpan.Zero, "Stall threshold must be positive.")
        .Validate(options => options.DeadLetterWindow > TimeSpan.Zero, "Dead letter window must be positive.")
        .ValidateOnStart();

    builder.Services.AddSingleton<ConsumerStatus>();
    builder.Services.AddSingleton<KafkaHealthCheck>();

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ConsumerDbContext>("database", tags: [ReadinessTag])
        .AddCheck<KafkaHealthCheck>("kafka", HealthStatus.Degraded)
        .AddCheck<ConsumerHealthCheck>("consumer", HealthStatus.Degraded)
        .AddCheck<WarehouseCircuitHealthCheck>("warehouse", HealthStatus.Degraded);

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ConsumerDbContext>().Database.MigrateAsync();
    }

    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains(ReadinessTag)
    });
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = HealthReportWriter.WriteAsync
    });

    await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Consumer terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
