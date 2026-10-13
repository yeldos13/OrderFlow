using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Consumer.Deduplication;

namespace OrderFlow.Consumer.Persistence.Configurations;

internal sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("processed_events");

        builder.HasKey(processedEvent => processedEvent.EventId);

        builder.Property(processedEvent => processedEvent.EventId)
            .ValueGeneratedNever();

        builder.Property(processedEvent => processedEvent.EventType)
            .HasMaxLength(200);

        builder.HasIndex(processedEvent => processedEvent.ProcessedAt);
    }
}
