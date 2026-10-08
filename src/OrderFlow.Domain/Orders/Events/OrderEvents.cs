using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Orders.Events;

public sealed record OrderCreatedDomainEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    IReadOnlyList<OrderLine> Lines,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderConfirmedDomainEvent(Guid OrderId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderShippedDomainEvent(Guid OrderId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderCancelledDomainEvent(Guid OrderId, DateTimeOffset OccurredAt) : IDomainEvent;
