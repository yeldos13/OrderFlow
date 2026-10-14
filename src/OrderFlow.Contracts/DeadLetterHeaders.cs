namespace OrderFlow.Contracts;

public static class DeadLetterHeaders
{
    public const string Reason = "dlq-reason";
    public const string ExceptionType = "dlq-exception-type";
    public const string OriginalTopic = "dlq-original-topic";
    public const string OriginalPartition = "dlq-original-partition";
    public const string OriginalOffset = "dlq-original-offset";
    public const string ConsumerGroup = "dlq-consumer-group";
    public const string FailedAt = "dlq-failed-at";
}
