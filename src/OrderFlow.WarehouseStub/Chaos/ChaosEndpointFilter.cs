namespace OrderFlow.WarehouseStub.Chaos;

internal sealed partial class ChaosEndpointFilter(ChaosState state, ILogger<ChaosEndpointFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var settings = state.Current;

        if (settings.Outage)
        {
            LogOutage();
            return Results.Problem("Warehouse is down.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (Random.Shared.NextDouble() < settings.SlowRate)
        {
            LogSlowResponse(settings.SlowDelayMs);
            await Task.Delay(settings.SlowDelayMs, context.HttpContext.RequestAborted);
        }

        if (Random.Shared.NextDouble() < settings.FailureRate)
        {
            LogRandomFailure();
            return Results.Problem("Warehouse failed to process the request.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return await next(context);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chaos: outage mode, rejecting request")]
    private partial void LogOutage();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chaos: delaying response by {DelayMs} ms")]
    private partial void LogSlowResponse(int delayMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chaos: random failure")]
    private partial void LogRandomFailure();
}
