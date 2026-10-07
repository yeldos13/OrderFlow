using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", table =>
        {
            table.HasCheckConstraint("ck_order_items_quantity_positive", "quantity > 0");
            table.HasCheckConstraint("ck_order_items_unit_price_positive", "unit_price > 0");
        });

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .ValueGeneratedNever();

        builder.Property(item => item.UnitPrice)
            .HasPrecision(18, 2);

        builder.Ignore(item => item.LineTotal);

        builder.HasIndex("OrderId", nameof(OrderItem.ProductId))
            .IsUnique();
    }
}
