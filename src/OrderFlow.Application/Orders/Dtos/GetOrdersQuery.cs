using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders.Dtos;

public sealed record GetOrdersQuery(
    OrderStatus? Status = null,
    Guid? CustomerId = null,
    int Page = 1,
    int PageSize = 20);
