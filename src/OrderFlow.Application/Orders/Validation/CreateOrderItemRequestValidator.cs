using FluentValidation;
using OrderFlow.Application.Orders.Dtos;

namespace OrderFlow.Application.Orders.Validation;

internal sealed class CreateOrderItemRequestValidator : AbstractValidator<CreateOrderItemRequest>
{
    public const int MaxQuantity = 10_000;

    public CreateOrderItemRequestValidator()
    {
        RuleFor(item => item.ProductId)
            .NotEmpty();

        RuleFor(item => item.Quantity)
            .InclusiveBetween(1, MaxQuantity);

        RuleFor(item => item.UnitPrice)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
    }
}
