using Microsoft.AspNetCore.Mvc.Filters;

namespace OrderFlow.Api.Idempotency;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class IdempotentAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        ActivatorUtilities.CreateInstance<IdempotencyFilter>(serviceProvider);
}
