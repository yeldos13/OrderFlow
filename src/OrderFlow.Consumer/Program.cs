using Microsoft.EntityFrameworkCore;
using OrderFlow.Consumer.Deduplication;
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
    builder.Services.AddScoped<ProcessedEventStore>();
    builder.Services.AddScoped<OrderEventHandler>();
    builder.Services.AddSingleton<DeadLetterPublisher>();
    builder.Services.AddHostedService<OrderEventsConsumer>();
    builder.Services.AddHostedService<ProcessedEventsCleanupService>();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ConsumerDbContext>().Database.MigrateAsync();
    }

    app.MapHealthChecks("/health/live");

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
