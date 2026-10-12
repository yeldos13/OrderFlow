using Microsoft.Extensions.Options;
using OrderFlow.Consumer.Kafka;
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
        .Validate(options => options.RetryDelay > TimeSpan.Zero, "Retry delay must be positive.")
        .ValidateOnStart();

    builder.Services.AddOptions<WarehouseOptions>()
        .BindConfiguration(WarehouseOptions.SectionName)
        .Validate(options => options.BaseUrl is { IsAbsoluteUri: true }, "Warehouse base URL is not configured.")
        .Validate(options => options.Timeout > TimeSpan.Zero, "Warehouse timeout must be positive.")
        .ValidateOnStart();

    builder.Services.AddHttpClient<WarehouseClient>((services, client) =>
    {
        var warehouse = services.GetRequiredService<IOptions<WarehouseOptions>>().Value;
        client.BaseAddress = warehouse.BaseUrl;
        client.Timeout = warehouse.Timeout;
    });

    builder.Services.AddScoped<OrderEventHandler>();
    builder.Services.AddHostedService<OrderEventsConsumer>();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

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
