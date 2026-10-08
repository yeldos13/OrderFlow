using System.Text.Json;

namespace OrderFlow.Contracts;

public static class EventSerializer
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
