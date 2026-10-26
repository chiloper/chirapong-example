using Microsoft.EntityFrameworkCore;
using Stock.Api.Domain;

namespace Stock.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockItem> Stocks => Set<StockItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(p => p.Id);
            e.Property(p => p.Sku).HasMaxLength(50).IsRequired();
            e.HasIndex(p => p.Sku).IsUnique();
            e.Property(p => p.Name).HasMaxLength(255).IsRequired();
            e.Property(p => p.Description).HasMaxLength(2000);
            e.Property(p => p.Price).HasPrecision(12, 2);
            e.Property(p => p.Category).HasMaxLength(100);
            e.Property(p => p.ImageUrl).HasMaxLength(500);
            e.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
            e.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");
            e.ToTable(t => t.HasCheckConstraint("ck_products_price", "price >= 0"));
        });

        modelBuilder.Entity<StockItem>(e =>
        {
            e.ToTable("stocks");
            e.HasKey(s => s.Sku);
            e.Property(s => s.Sku).HasMaxLength(50);
            e.Ignore(s => s.Available);
            e.Property(s => s.Version)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
            e.HasOne(s => s.Product)
                .WithOne(p => p.Stock)
                .HasForeignKey<StockItem>(s => s.Sku)
                .HasPrincipalKey<Product>(p => p.Sku)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_stocks_on_hand", "quantity_on_hand >= 0");
                t.HasCheckConstraint("ck_stocks_reserved", "quantity_reserved >= 0 AND quantity_reserved <= quantity_on_hand");
            });
        });

        modelBuilder.Entity<Cart>(e =>
        {
            e.ToTable("carts");
            e.HasKey(c => c.Id);
            e.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
            e.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");
            e.HasIndex(c => c.UpdatedAt);
        });

        modelBuilder.Entity<CartItem>(e =>
        {
            e.ToTable("cart_items");
            e.HasKey(i => new { i.CartId, i.Sku });
            e.Property(i => i.Sku).HasMaxLength(50);
            e.Property(i => i.AddedAt).HasDefaultValueSql("now()");
            e.HasOne(i => i.Cart).WithMany(c => c.Items).HasForeignKey(i => i.CartId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.Sku).HasPrincipalKey(p => p.Sku).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_cart_items_quantity", "quantity > 0"));
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.HasKey(o => o.Id);
            e.HasIndex(o => o.PublicId).IsUnique();
            e.HasIndex(o => new { o.Status, o.ExpiresAt });
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.Total).HasPrecision(12, 2);
            e.Property(o => o.CustomerName).HasMaxLength(200).IsRequired();
            e.Property(o => o.CustomerEmail).HasMaxLength(255).IsRequired();
            e.Property(o => o.CustomerPhone).HasMaxLength(50);
            e.Property(o => o.ShippingAddress).HasMaxLength(1000);
            e.Property(o => o.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");
            e.HasKey(i => new { i.OrderId, i.Sku });
            e.Property(i => i.Sku).HasMaxLength(50);
            e.Property(i => i.NameSnapshot).HasMaxLength(255).IsRequired();
            e.Property(i => i.UnitPriceSnapshot).HasPrecision(12, 2);
            e.HasOne(i => i.Order).WithMany(o => o.Items).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("ck_order_items_quantity", "quantity > 0"));
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.HasKey(p => p.Id);
            e.Property(p => p.Provider).HasMaxLength(50).IsRequired();
            e.Property(p => p.ProviderRef).HasMaxLength(100).IsRequired();
            e.HasIndex(p => p.ProviderRef).IsUnique();
            e.Property(p => p.PaymentUrl).HasMaxLength(500).IsRequired();
            e.Property(p => p.Amount).HasPrecision(12, 2);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
            // At most one open payment attempt per order, so a double click cannot start two.
            e.HasIndex(p => p.OrderId).IsUnique().HasFilter("status = 'Pending'").HasDatabaseName("ux_payments_order_pending");
            e.HasOne(p => p.Order).WithMany().HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentEvent>(e =>
        {
            e.ToTable("payment_events");
            e.HasKey(p => p.EventId);
            e.Property(p => p.EventId).HasMaxLength(100);
            e.Property(p => p.Type).HasMaxLength(50).IsRequired();
            e.Property(p => p.ReceivedAt).HasDefaultValueSql("now()");
            e.HasOne(p => p.Payment).WithMany().HasForeignKey(p => p.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(e =>
        {
            e.ToTable("stock_movements");
            e.HasKey(m => m.Id);
            e.Property(m => m.Sku).HasMaxLength(50).IsRequired();
            e.Property(m => m.Reason).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.Note).HasMaxLength(500);
            e.Property(m => m.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(m => m.Sku);
            e.HasOne<Product>()
                .WithMany()
                .HasForeignKey(m => m.Sku)
                .HasPrincipalKey(p => p.Sku)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
