using OrderFlow.Application.Orders.Dtos;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

internal static class OrderMappings
{
    public static OrderResponse ToResponse(this Order order) => new(
        order.Id,
        order.CustomerId,
        order.Status,
        order.TotalAmount,
        order.CreatedAt,
        order.UpdatedAt,
        order.Items
            .Select(item => new OrderItemResponse(item.ProductId, item.Quantity, item.UnitPrice, item.LineTotal))
            .ToList());

    public static OrderLine ToOrderLine(this CreateOrderItemRequest item) =>
        new(item.ProductId, item.Quantity, item.UnitPrice);
}
