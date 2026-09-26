using Microsoft.EntityFrameworkCore;
using ProductShop.Shared;

namespace ProductShop.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<StockEntry> StockEntries => Set<StockEntry>();
    public DbSet<StockEntryItem> StockEntryItems => Set<StockEntryItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Shob taka/amount column decimal(18,2)
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Brand>(e =>
        {
            e.Property(b => b.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(b => b.Name).IsUnique();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.Code).HasMaxLength(30);
            e.Property(p => p.Description).HasMaxLength(500);
            e.HasIndex(p => p.Code);

            // Je category / brand e product ache, take delete kora jabe na
            e.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Brand).WithMany().HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.Restrict);

            e.HasMany(p => p.Variants)
             .WithOne()
             .HasForeignKey(v => v.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductVariant>(e =>
        {
            e.Property(v => v.Size).HasMaxLength(20).IsRequired();
            e.Property(v => v.Color).HasMaxLength(30).IsRequired();
            e.Property(v => v.Sku).HasMaxLength(40);
            e.HasIndex(v => v.Sku).IsUnique();
            // Ek product e same size + color duibar thakbe na
            e.HasIndex(v => new { v.ProductId, v.Size, v.Color }).IsUnique();
        });

        modelBuilder.Entity<StockEntry>(e =>
        {
            e.HasMany(s => s.Items)
             .WithOne()
             .HasForeignKey(i => i.StockEntryId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => s.EntryDate);
        });

        modelBuilder.Entity<StockEntryItem>(e =>
        {
            // Je product er stock entry ache, take delete kora jabe na
            e.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ProductVariant>().WithMany().HasForeignKey(i => i.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.Phone).HasMaxLength(20).IsRequired();
            e.Property(c => c.Address).HasMaxLength(200);
            e.HasIndex(c => c.Phone).IsUnique();
        });

        modelBuilder.Entity<Sale>(e =>
        {
            e.Property(s => s.InvoiceNo).IsRequired();
            e.HasMany(s => s.Items)
             .WithOne()
             .HasForeignKey(i => i.SaleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Payments)
             .WithOne()
             .HasForeignKey(p => p.SaleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Customer>().WithMany().HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(s => s.SaleDate);
        });

        modelBuilder.Entity<SaleItem>(e =>
        {
            e.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ProductVariant>().WithMany().HasForeignKey(i => i.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalePayment>(e =>
        {
            e.HasIndex(p => p.PaymentDate);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("Users");
            e.Property(u => u.Username).HasMaxLength(50).IsRequired();
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Role).HasMaxLength(20).IsRequired();
            e.HasIndex(u => u.Username).IsUnique();
        });
    }
}
