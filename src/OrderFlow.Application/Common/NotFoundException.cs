namespace OrderFlow.Application.Common;

public sealed class NotFoundException(string resourceName, object key)
    : Exception($"{resourceName} '{key}' was not found.")
{
    public string ResourceName { get; } = resourceName;

    public object Key { get; } = key;
}
