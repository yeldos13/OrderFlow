using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly.CircuitBreaker;

namespace OrderFlow.Consumer.Health;

internal sealed class WarehouseCircuitHealthCheck(CircuitBreakerStateProvider stateProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var state = stateProvider.CircuitState;
        var data = new Dictionary<string, object> { ["state"] = state.ToString() };

        return Task.FromResult(state is CircuitState.Closed or CircuitState.HalfOpen
            ? HealthCheckResult.Healthy($"Warehouse circuit is {state}.", data)
            : new HealthCheckResult(context.Registration.FailureStatus, $"Warehouse circuit is {state}.", data: data));
    }
}
