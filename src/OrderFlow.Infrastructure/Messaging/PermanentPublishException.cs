namespace OrderFlow.Infrastructure.Messaging;

public sealed class PermanentPublishException(string message, Exception innerException)
    : Exception(message, innerException);
