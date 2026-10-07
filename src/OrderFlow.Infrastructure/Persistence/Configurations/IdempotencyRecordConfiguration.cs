using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Application.Idempotency;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");

        builder.HasKey(record => record.Key);

        builder.Property(record => record.Key)
            .HasMaxLength(IdempotencyRecord.MaxKeyLength);

        builder.Property(record => record.RequestHash)
            .HasMaxLength(64)
            .IsFixedLength();

        builder.Property(record => record.ResponseContentType)
            .HasMaxLength(256);

        builder.Property(record => record.ResponseLocation)
            .HasMaxLength(2048);

        builder.HasIndex(record => record.CreatedAt);
    }
}
