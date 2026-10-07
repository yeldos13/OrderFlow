using FluentValidation;
using OrderFlow.Application.Orders.Dtos;

namespace OrderFlow.Application.Orders.Validation;

internal sealed class GetOrdersQueryValidator : AbstractValidator<GetOrdersQuery>
{
    public const int MaxPageSize = 100;

    public GetOrdersQueryValidator()
    {
        RuleFor(query => query.Status)
            .IsInEnum();

        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, MaxPageSize);
    }
}
