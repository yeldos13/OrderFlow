namespace OrderFlow.Consumer.Warehouse;

public interface IWarehouseClient
{
    Task SendAsync(WarehouseOrderEvent orderEvent, CancellationToken cancellationToken);
}
