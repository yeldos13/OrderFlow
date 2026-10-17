using OrderFlow.Application.Orders;
using OrderFlow.Application.Orders.Dtos;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Tests.Orders;

public sealed class OrderMappingsTests
{
    [Fact]
    public void ToResponse_CopiesOrderAndItems()
    {
        var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var line = new OrderLine(Guid.NewGuid(), 3, 2.5m);
        var order = Order.Create(Guid.NewGuid(), [line], now);

        var response = order.ToResponse();

        Assert.Equal(order.Id, response.Id);
        Assert.Equal(order.CustomerId, response.CustomerId);
        Assert.Equal(OrderStatus.Created, response.Status);
        Assert.Equal(7.5m, response.TotalAmount);
        Assert.Equal(now, response.CreatedAt);
        Assert.Equal(now, response.UpdatedAt);

        var item = Assert.Single(response.Items);
        Assert.Equal(new OrderItemResponse(line.ProductId, 3, 2.5m, 7.5m), item);
    }

    [Fact]
    public void ToOrderLine_CopiesItemFields()
    {
        var request = new CreateOrderItemRequest(Guid.NewGuid(), 2, 9.99m);

        Assert.Equal(new OrderLine(request.ProductId, 2, 9.99m), request.ToOrderLine());
    }
}
