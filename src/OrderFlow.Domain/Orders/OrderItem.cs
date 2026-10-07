namespace OrderFlow.Domain.Orders;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    internal OrderItem(Guid productId, int quantity, decimal unitPrice)
    {
        if (productId == Guid.Empty)
        {
            throw new OrderValidationException("Product id is required.");
        }

        if (quantity <= 0)
        {
            throw new OrderValidationException($"Quantity for product {productId} must be greater than zero.");
        }

        if (unitPrice <= 0)
        {
            throw new OrderValidationException($"Unit price for product {productId} must be greater than zero.");
        }

        Id = Guid.CreateVersion7();
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
