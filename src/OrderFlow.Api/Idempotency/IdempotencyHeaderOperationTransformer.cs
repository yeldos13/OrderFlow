using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using OrderFlow.Application.Idempotency;

namespace OrderFlow.Api.Idempotency;

internal sealed class IdempotencyHeaderOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<IdempotentAttribute>().Any())
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotencyFilter.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Unique client-generated key that makes the request safe to retry.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MaxLength = IdempotencyRecord.MaxKeyLength
            }
        });

        return Task.CompletedTask;
    }
}
