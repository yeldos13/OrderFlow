using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Orders.Events;
using static OrderFlow.Domain.Tests.Orders.OrderTestData;

namespace OrderFlow.Domain.Tests.Orders;

public sealed class OrderCreationTests
{
    [Fact]
    public void Create_WithValidLines_InitializesOrder()
    {
        var customerId = Guid.NewGuid();
        var first = Line(quantity: 2, unitPrice: 10.50m);
        var second = Line(quantity: 3, unitPrice: 4m);

        var order = Order.Create(customerId, [first, second], CreatedAt);

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(7, order.Id.Version);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Equal(33m, order.TotalAmount);
        Assert.Equal(CreatedAt, order.CreatedAt);
        Assert.Equal(CreatedAt, order.UpdatedAt);
        Assert.Equal(2, order.Items.Count);
    }

    [Fact]
    public void Create_MapsLinesToItems()
    {
        var line = Line(quantity: 4, unitPrice: 2.25m);

        var item = Assert.Single(CreateOrder(line).Items);

        Assert.Equal(line.ProductId, item.ProductId);
        Assert.Equal(4, item.Quantity);
        Assert.Equal(2.25m, item.UnitPrice);
        Assert.Equal(9m, item.LineTotal);
    }

    [Fact]
    public void Create_RaisesOrderCreatedEvent()
    {
        var line = Line(quantity: 2, unitPrice: 5m);

        var order = CreateOrder(line);

        var domainEvent = Assert.IsType<OrderCreatedDomainEvent>(Assert.Single(order.DomainEvents));
        Assert.Equal(order.Id, domainEvent.OrderId);
        Assert.Equal(order.CustomerId, domainEvent.CustomerId);
        Assert.Equal(10m, domainEvent.TotalAmount);
        Assert.Equal(CreatedAt, domainEvent.OccurredAt);
        Assert.Equal(line, Assert.Single(domainEvent.Lines));
    }

    [Fact]
    public void Create_WithEmptyCustomer_Throws()
    {
        var exception = Assert.Throws<OrderValidationException>(() => Order.Create(Guid.Empty, [Line()], CreatedAt));

        Assert.Contains("Customer", exception.Message);
    }

    [Fact]
    public void Create_WithoutLines_Throws()
    {
        Assert.Throws<OrderValidationException>(() => Order.Create(Guid.NewGuid(), [], CreatedAt));
    }

    [Fact]
    public void Create_WithNullLines_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Order.Create(Guid.NewGuid(), null!, CreatedAt));
    }

    [Fact]
    public void Create_WithDuplicateProduct_Throws()
    {
        var productId = Guid.NewGuid();

        var exception = Assert.Throws<OrderValidationException>(() => Order.Create(
            Guid.NewGuid(),
            [new OrderLine(productId, 1, 1m), new OrderLine(productId, 2, 1m)],
            CreatedAt));

        Assert.Contains(productId.ToString(), exception.Message);
    }

    [Fact]
    public void Create_WithEmptyProduct_Throws()
    {
        Assert.Throws<OrderValidationException>(() => CreateOrder(new OrderLine(Guid.Empty, 1, 1m)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveQuantity_Throws(int quantity)
    {
        Assert.Throws<OrderValidationException>(() => CreateOrder(Line(quantity: quantity)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    public void Create_WithNonPositiveUnitPrice_Throws(decimal unitPrice)
    {
        Assert.Throws<OrderValidationException>(() => CreateOrder(Line(unitPrice: unitPrice)));
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        Assert.NotEqual(CreateOrder().Id, CreateOrder().Id);
    }

    [Fact]
    public void ClearDomainEvents_RemovesRaisedEvents()
    {
        var order = CreateOrder();

        order.ClearDomainEvents();

        Assert.Empty(order.DomainEvents);
    }
}
