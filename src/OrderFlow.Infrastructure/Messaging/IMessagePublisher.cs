namespace OrderFlow.Infrastructure.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken);
}
