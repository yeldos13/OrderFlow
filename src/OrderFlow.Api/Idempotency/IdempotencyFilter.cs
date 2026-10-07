using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using OrderFlow.Application.Idempotency;

namespace OrderFlow.Api.Idempotency;

internal sealed class IdempotencyFilter(
    IIdempotencyStore store,
    ProblemDetailsFactory problemDetailsFactory) : IAsyncResourceFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var cancellationToken = httpContext.RequestAborted;
        var key = httpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(key) || key.Length > IdempotencyRecord.MaxKeyLength)
        {
            context.Result = Problem(
                httpContext,
                StatusCodes.Status400BadRequest,
                "Invalid idempotency key",
                $"The {HeaderName} header is required and must not exceed {IdempotencyRecord.MaxKeyLength} characters.");
            return;
        }

        var requestHash = await ComputeRequestHashAsync(httpContext.Request, cancellationToken);

        var existing = await store.FindAsync(key, cancellationToken);
        if (existing is not null)
        {
            context.Result = Replay(httpContext, existing, requestHash);
            return;
        }

        await using var scope = await store.TryBeginAsync(key, requestHash, cancellationToken);
        if (scope is null)
        {
            existing = await store.FindAsync(key, cancellationToken);
            context.Result = existing is null
                ? Problem(
                    httpContext,
                    StatusCodes.Status409Conflict,
                    "Request in progress",
                    $"A request with the same {HeaderName} is still being processed. Retry later.")
                : Replay(httpContext, existing, requestHash);
            return;
        }

        var response = httpContext.Response;
        var originalBody = response.Body;
        await using var buffer = new MemoryStream();
        response.Body = buffer;

        try
        {
            var executedContext = await next();

            if (executedContext.Exception is not null && !executedContext.ExceptionHandled)
            {
                return;
            }

            if (IsSuccessStatusCode(response.StatusCode))
            {
                var location = response.Headers.Location.ToString();

                await scope.CompleteAsync(
                    response.StatusCode,
                    response.ContentType,
                    Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length),
                    string.IsNullOrEmpty(location) ? null : location,
                    cancellationToken);
            }
        }
        finally
        {
            response.Body = originalBody;
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody, cancellationToken);
    }

    private IActionResult Replay(HttpContext httpContext, IdempotencyRecord record, string requestHash)
    {
        if (record.RequestHash != requestHash)
        {
            return Problem(
                httpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Idempotency key reused",
                $"The {HeaderName} has already been used for a different request.");
        }

        httpContext.Response.Headers[ReplayedHeaderName] = "true";

        if (record.ResponseLocation is not null)
        {
            httpContext.Response.Headers.Location = record.ResponseLocation;
        }

        return new ContentResult
        {
            StatusCode = record.ResponseStatusCode,
            ContentType = record.ResponseContentType,
            Content = record.ResponseBody
        };
    }

    private ObjectResult Problem(HttpContext httpContext, int statusCode, string title, string detail)
    {
        var problemDetails = problemDetailsFactory.CreateProblemDetails(httpContext, statusCode, title, detail: detail);

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static async Task<string> ComputeRequestHashAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes($"{request.Method} {request.Path}\n"));

        var chunk = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            hash.AppendData(chunk, 0, read);
        }

        request.Body.Position = 0;

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static bool IsSuccessStatusCode(int statusCode) => statusCode is >= 200 and < 300;
}
