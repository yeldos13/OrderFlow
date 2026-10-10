using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrderFlow.Api.Health;

internal static class HealthReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static Task WriteAsync(HttpContext httpContext, HealthReport report)
    {
        var response = new
        {
            Status = report.Status.ToString(),
            DurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            Checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    Status = entry.Value.Status.ToString(),
                    entry.Value.Description,
                    DurationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                    Data = entry.Value.Data.Count > 0 ? entry.Value.Data : null
                })
        };

        httpContext.Response.ContentType = "application/json; charset=utf-8";
        return httpContext.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }
}
