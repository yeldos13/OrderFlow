using OrderFlow.WarehouseStub.Chaos;
using OrderFlow.WarehouseStub.Events;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

var initialChaos = builder.Configuration.GetSection(ChaosSettings.SectionName).Get<ChaosSettings>() ?? new ChaosSettings();
var chaosErrors = initialChaos.Validate().ToList();
if (chaosErrors.Count > 0)
{
    throw new InvalidOperationException($"Invalid chaos settings: {string.Join(" ", chaosErrors)}");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new ChaosState(initialChaos));
builder.Services.AddSingleton<ReceivedEventStore>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

var events = app.MapGroup("/api/order-events");

events.MapPost("/", (OrderEventRequest request, ReceivedEventStore store, ILogger<Program> logger) =>
    {
        if (request.EventId == Guid.Empty || string.IsNullOrWhiteSpace(request.EventType))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["event"] = ["EventId and EventType are required."]
            });
        }

        var received = store.Record(request);

        logger.LogInformation(
            "Received {EventType} {EventId} for order {OrderId}, delivery #{Delivery}",
            received.EventType,
            received.EventId,
            received.OrderId,
            received.Deliveries);

        return Results.Accepted();
    })
    .AddEndpointFilter<ChaosEndpointFilter>();

events.MapGet("/", (ReceivedEventStore store) =>
{
    var received = store.GetAll();

    return Results.Ok(new
    {
        Total = received.Count,
        Duplicates = received.Count(item => item.Deliveries > 1),
        Events = received
    });
});

events.MapDelete("/", (ReceivedEventStore store) =>
{
    store.Clear();
    return Results.NoContent();
});

app.MapGet("/api/chaos", (ChaosState state) => state.Current);

app.MapPut("/api/chaos", (ChaosSettings settings, ChaosState state) =>
{
    var errors = settings.Validate().ToList();
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["chaos"] = [.. errors] });
    }

    state.Update(settings);
    return Results.Ok(settings);
});

app.MapHealthChecks("/health/live");

app.Run();
