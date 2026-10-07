using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Common;
using OrderFlow.Application.Orders.Dtos;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Orders;

internal sealed partial class OrderService(
    IOrderFlowDbContext dbContext,
    IValidator<CreateOrderRequest> createOrderValidator,
    IValidator<GetOrdersQuery> getOrdersValidator,
    TimeProvider timeProvider,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        await createOrderValidator.ValidateAndThrowAsync(request, cancellationToken);

        var lines = request.Items.Select(item => item.ToOrderLine()).ToList();
        var order = Order.Create(request.CustomerId, lines, timeProvider.GetUtcNow());

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogOrderCreated(order.Id, order.CustomerId, order.Items.Count, order.TotalAmount);

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

        var previousStatus = order.Status;
        change(order, timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);

        LogOrderStatusChanged(order.Id, previousStatus, order.Status);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} created for customer {CustomerId} with {ItemCount} items, total {TotalAmount}")]
    private partial void LogOrderCreated(Guid orderId, Guid customerId, int itemCount, decimal totalAmount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} status changed from {PreviousStatus} to {Status}")]
    private partial void LogOrderStatusChanged(Guid orderId, OrderStatus previousStatus, OrderStatus status);
}
