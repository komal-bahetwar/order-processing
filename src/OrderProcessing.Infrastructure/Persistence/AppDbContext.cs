using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Idempotency;
using OrderProcessing.Domain;

namespace OrderProcessing.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders", table => table.HasCheckConstraint(
                "ck_orders_status",
                "status IN ('PENDING', 'PROCESSING', 'SHIPPED', 'DELIVERED', 'CANCELLED')"));

            entity.HasKey(order => order.Id).HasName("pk_orders");

            entity.Property(order => order.Id).HasColumnName("id");
            entity.Property(order => order.Status).HasColumnName("status");
            entity.Property(order => order.TotalAmount).HasColumnName("total_amount");
            entity.Property(order => order.CreatedAt).HasColumnName("created_at");
            entity.Property(order => order.UpdatedAt).HasColumnName("updated_at");
            entity.Property(order => order.Version).HasColumnName("version");

            entity.Property(order => order.Status)
                .HasConversion(
                    status => status.ToString().ToUpperInvariant(),
                    value => Enum.Parse<OrderStatus>(value, ignoreCase: true))
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(order => order.TotalAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(order => order.CreatedAt).HasColumnType("timestamptz").IsRequired();
            entity.Property(order => order.UpdatedAt).HasColumnType("timestamptz");
            entity.Property(order => order.Version)
                .IsConcurrencyToken()
                .HasDefaultValue(0)
                .IsRequired();

            entity.HasMany(order => order.Items)
                .WithOne()
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(order => order.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasIndex(order => new { order.Status, order.CreatedAt, order.Id })
                .HasDatabaseName("ix_orders_status_created_at");
            entity.HasIndex(order => new { order.CreatedAt, order.Id })
                .HasDatabaseName("ix_orders_created_at");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items", table =>
            {
                table.HasCheckConstraint("ck_order_items_quantity", "quantity > 0");
                table.HasCheckConstraint("ck_order_items_unit_price", "unit_price >= 0");
            });

            entity.HasKey(item => item.Id).HasName("pk_order_items");

            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.OrderId).HasColumnName("order_id");
            entity.Property(item => item.ProductId).HasColumnName("product_id");
            entity.Property(item => item.Quantity).HasColumnName("quantity");
            entity.Property(item => item.UnitPrice).HasColumnName("unit_price");
            entity.Property(item => item.OrderId).IsRequired();
            entity.Property(item => item.ProductId).IsRequired();
            entity.Property(item => item.Quantity).IsRequired();
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2).IsRequired();
            entity.Ignore(item => item.LineTotal);

            entity.HasIndex(item => item.OrderId).HasDatabaseName("ix_order_items_order_id");
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("idempotency_records");

            entity.HasKey(record => record.Id).HasName("pk_idempotency_records");

            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Scope).HasColumnName("scope").HasMaxLength(64).IsRequired();
            entity.Property(record => record.Key).HasColumnName("key").HasMaxLength(128).IsRequired();
            entity.Property(record => record.RequestFingerprint).HasColumnName("request_fingerprint").HasMaxLength(128).IsRequired();
            entity.Property(record => record.OrderId).HasColumnName("order_id").IsRequired();
            entity.Property(record => record.ResponseBody).HasColumnName("response_body").IsRequired();
            entity.Property(record => record.ContentType).HasColumnName("content_type").HasMaxLength(128).IsRequired();
            entity.Property(record => record.StatusCode).HasColumnName("status_code").IsRequired();
            entity.Property(record => record.Location).HasColumnName("location").HasMaxLength(256).IsRequired();
            entity.Property(record => record.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            entity.Property(record => record.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();

            entity.HasIndex(record => new { record.Scope, record.Key })
                .IsUnique()
                .HasDatabaseName("ux_idempotency_records_scope_key");
            entity.HasIndex(record => record.ExpiresAt).HasDatabaseName("ix_idempotency_records_expires_at");
        });
    }
}
