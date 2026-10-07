namespace OrderFlow.Application.Orders.Dtos;

public sealed record CreateOrderRequest(Guid CustomerId, IReadOnlyList<CreateOrderItemRequest> Items);

public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity, decimal UnitPrice);
