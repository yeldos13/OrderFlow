using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(order => order.Id);

        builder.Property(order => order.Id)
            .ValueGeneratedNever();

        builder.Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(order => order.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property<uint>("Version")
            .IsRowVersion();

        builder.Ignore(order => order.DomainEvents);

        builder.HasMany(order => order.Items)
            .WithOne()
            .HasForeignKey("OrderId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(order => order.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(order => order.CustomerId);

        builder.HasIndex(order => new { order.Status, order.CreatedAt });
    }
}
