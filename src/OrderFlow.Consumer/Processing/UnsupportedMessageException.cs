namespace OrderFlow.Consumer.Processing;

public sealed class UnsupportedMessageException : Exception
{
    public UnsupportedMessageException(string message)
        : base(message)
    {
    }

    public UnsupportedMessageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
