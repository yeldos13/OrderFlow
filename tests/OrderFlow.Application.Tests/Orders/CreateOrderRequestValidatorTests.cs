using FluentValidation.TestHelper;
using OrderFlow.Application.Orders.Dtos;
using OrderFlow.Application.Orders.Validation;

namespace OrderFlow.Application.Tests.Orders;

public sealed class CreateOrderRequestValidatorTests
{
    private readonly CreateOrderRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_HasNoErrors()
    {
        var result = _validator.TestValidate(Request(Item(), Item()));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyCustomer_IsRejected()
    {
        var result = _validator.TestValidate(new CreateOrderRequest(Guid.Empty, [Item()]));

        result.ShouldHaveValidationErrorFor(request => request.CustomerId);
    }

    [Fact]
    public void EmptyItems_AreRejected()
    {
        var result = _validator.TestValidate(Request());

        result.ShouldHaveValidationErrorFor(request => request.Items);
    }

    [Fact]
    public void NullItems_AreRejectedWithoutThrowing()
    {
        var result = _validator.TestValidate(new CreateOrderRequest(Guid.NewGuid(), null!));

        result.ShouldHaveValidationErrorFor(request => request.Items);
    }

    [Fact]
    public void TooManyItems_AreRejected()
    {
        var items = Enumerable.Range(0, CreateOrderRequestValidator.MaxItems + 1).Select(_ => Item()).ToArray();

        var result = _validator.TestValidate(Request(items));

        result.ShouldHaveValidationErrorFor(request => request.Items)
            .WithErrorMessage($"Order cannot contain more than {CreateOrderRequestValidator.MaxItems} items.");
    }

    [Fact]
    public void DuplicateProducts_AreRejected()
    {
        var productId = Guid.NewGuid();

        var result = _validator.TestValidate(Request(Item(productId), Item(productId)));

        result.ShouldHaveValidationErrorFor(request => request.Items)
            .WithErrorMessage("Each product can appear in the order only once.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(CreateOrderItemRequestValidator.MaxQuantity + 1)]
    public void QuantityOutOfRange_IsRejected(int quantity)
    {
        var result = _validator.TestValidate(Request(Item(quantity: quantity)));

        result.ShouldHaveValidationErrorFor("Items[0].Quantity");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.001")]
    public void InvalidUnitPrice_IsRejected(string unitPrice)
    {
        var result = _validator.TestValidate(Request(Item(unitPrice: decimal.Parse(unitPrice, System.Globalization.CultureInfo.InvariantCulture))));

        result.ShouldHaveValidationErrorFor("Items[0].UnitPrice");
    }

    [Fact]
    public void EmptyProduct_IsRejected()
    {
        var result = _validator.TestValidate(Request(Item(Guid.Empty)));

        result.ShouldHaveValidationErrorFor("Items[0].ProductId");
    }

    private static CreateOrderRequest Request(params CreateOrderItemRequest[] items) => new(Guid.NewGuid(), items);

    private static CreateOrderItemRequest Item(Guid? productId = null, int quantity = 1, decimal unitPrice = 10m) =>
        new(productId ?? Guid.NewGuid(), quantity, unitPrice);
}
