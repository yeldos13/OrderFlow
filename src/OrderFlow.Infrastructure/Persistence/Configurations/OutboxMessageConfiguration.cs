using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Infrastructure.Outbox;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .ValueGeneratedNever();

        builder.Property(message => message.Type)
            .HasMaxLength(200);

        builder.Property(message => message.Payload)
            .HasColumnType("jsonb");

        builder.Property(message => message.TraceParent)
            .HasMaxLength(128);

        builder.Property(message => message.LastError)
            .HasMaxLength(OutboxMessage.MaxErrorLength);

        builder.HasIndex(message => new { message.OccurredAt, message.Id })
            .HasFilter("processed_at IS NULL AND failed_at IS NULL");

        builder.HasIndex(message => message.ProcessedAt)
            .HasFilter("processed_at IS NOT NULL");
    }
}
