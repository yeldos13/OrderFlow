using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Idempotency;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Outbox;

namespace OrderFlow.Infrastructure.Persistence;

public sealed class OrderFlowDbContext(DbContextOptions<OrderFlowDbContext> options)
    : DbContext(options), IOrderFlowDbContext
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderFlowDbContext).Assembly);
    }
}
