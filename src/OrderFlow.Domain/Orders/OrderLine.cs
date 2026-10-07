namespace OrderFlow.Domain.Orders;

public sealed record OrderLine(Guid ProductId, int Quantity, decimal UnitPrice);
