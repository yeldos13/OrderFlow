using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Abstractions;

public interface IOrderFlowDbContext
{
    DbSet<Order> Orders { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
