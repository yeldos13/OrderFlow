using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.Orders;

public sealed class OrderValidationException(string message) : DomainException(message);
