using Confluent.Kafka;

namespace OrderFlow.Consumer.Kafka;

internal static class KafkaLogLevels
{
    public static LogLevel ToLogLevel(this SyslogLevel level) => level switch
    {
        SyslogLevel.Emergency or SyslogLevel.Alert or SyslogLevel.Critical => LogLevel.Critical,
        SyslogLevel.Error => LogLevel.Error,
        SyslogLevel.Warning => LogLevel.Warning,
        SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
        _ => LogLevel.Debug
    };
}
