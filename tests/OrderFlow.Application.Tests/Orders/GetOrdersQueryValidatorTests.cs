using FluentValidation.TestHelper;
using OrderFlow.Application.Orders.Dtos;
using OrderFlow.Application.Orders.Validation;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Tests.Orders;

public sealed class GetOrdersQueryValidatorTests
{
    private readonly GetOrdersQueryValidator _validator = new();

    [Fact]
    public void DefaultQuery_IsValid()
    {
        _validator.TestValidate(new GetOrdersQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void QueryWithFilters_IsValid()
    {
        var query = new GetOrdersQuery(OrderStatus.Shipped, Guid.NewGuid(), 3, GetOrdersQueryValidator.MaxPageSize);

        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositivePage_IsRejected(int page)
    {
        _validator.TestValidate(new GetOrdersQuery(Page: page)).ShouldHaveValidationErrorFor(query => query.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetOrdersQueryValidator.MaxPageSize + 1)]
    public void PageSizeOutOfRange_IsRejected(int pageSize)
    {
        _validator.TestValidate(new GetOrdersQuery(PageSize: pageSize)).ShouldHaveValidationErrorFor(query => query.PageSize);
    }

    [Fact]
    public void UnknownStatus_IsRejected()
    {
        _validator.TestValidate(new GetOrdersQuery((OrderStatus)42)).ShouldHaveValidationErrorFor(query => query.Status);
    }
}
