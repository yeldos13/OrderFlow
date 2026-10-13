using Microsoft.EntityFrameworkCore;
using OrderFlow.Consumer.Deduplication;

namespace OrderFlow.Consumer.Persistence;

public sealed class ConsumerDbContext(DbContextOptions<ConsumerDbContext> options) : DbContext(options)
{
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConsumerDbContext).Assembly);
    }
}
