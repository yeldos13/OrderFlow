using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.Orders;

public sealed class InvalidOrderStateException(Guid orderId, OrderStatus currentStatus, OrderStatus targetStatus)
    : DomainException($"Order {orderId} cannot transition from {currentStatus} to {targetStatus}.")
{
    public Guid OrderId { get; } = orderId;

    public OrderStatus CurrentStatus { get; } = currentStatus;

    public OrderStatus TargetStatus { get; } = targetStatus;
}
