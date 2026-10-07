using OrderFlow.Application.Common;
using OrderFlow.Application.Orders.Dtos;

namespace OrderFlow.Application.Orders;

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken);

    Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResponse<OrderSummaryResponse>> GetListAsync(GetOrdersQuery query, CancellationToken cancellationToken);

    Task ConfirmAsync(Guid id, CancellationToken cancellationToken);

    Task ShipAsync(Guid id, CancellationToken cancellationToken);

    Task CancelAsync(Guid id, CancellationToken cancellationToken);
}
