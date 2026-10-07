using FluentValidation;
using OrderFlow.Application.Orders.Dtos;

namespace OrderFlow.Application.Orders.Validation;

internal sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public const int MaxItems = 100;

    public CreateOrderRequestValidator()
    {
        RuleFor(request => request.CustomerId)
            .NotEmpty();

        RuleFor(request => request.Items)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(items => items.Count <= MaxItems)
            .WithMessage($"Order cannot contain more than {MaxItems} items.")
            .Must(items => items.Select(item => item.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Each product can appear in the order only once.");

        RuleForEach(request => request.Items)
            .SetValidator(new CreateOrderItemRequestValidator());
    }
}
