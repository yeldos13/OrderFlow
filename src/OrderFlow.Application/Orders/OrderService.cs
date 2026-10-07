using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Common;
using OrderFlow.Application.Orders.Dtos;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

internal sealed class OrderService(
    IOrderFlowDbContext dbContext,
    IValidator<CreateOrderRequest> createOrderValidator,
    IValidator<GetOrdersQuery> getOrdersValidator,
    TimeProvider timeProvider) : IOrderService
{
    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        await createOrderValidator.ValidateAndThrowAsync(request, cancellationToken);

        var lines = request.Items.Select(item => item.ToOrderLine()).ToList();
        var order = Order.Create(request.CustomerId, lines, timeProvider.GetUtcNow());

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        return order.ToResponse();
    }

    public async Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), id);

        return order.ToResponse();
    }

    public async Task<PagedResponse<OrderSummaryResponse>> GetListAsync(GetOrdersQuery query, CancellationToken cancellationToken)
    {
        await getOrdersValidator.ValidateAndThrowAsync(query, cancellationToken);

        var orders = dbContext.Orders.AsNoTracking();

        if (query.Status is { } status)
        {
            orders = orders.Where(order => order.Status == status);
        }

        if (query.CustomerId is { } customerId)
        {
            orders = orders.Where(order => order.CustomerId == customerId);
        }

        var totalCount = await orders.CountAsync(cancellationToken);

        var items = await orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(order => new OrderSummaryResponse(
                order.Id,
                order.CustomerId,
                order.Status,
                order.TotalAmount,
                order.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<OrderSummaryResponse>(items, query.Page, query.PageSize, totalCount);
    }

    public Task ConfirmAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, (order, now) => order.Confirm(now), cancellationToken);

    public Task ShipAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, (order, now) => order.Ship(now), cancellationToken);

    public Task CancelAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, (order, now) => order.Cancel(now), cancellationToken);

    private async Task ChangeStatusAsync(Guid id, Action<Order, DateTimeOffset> change, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), id);

        change(order, timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
