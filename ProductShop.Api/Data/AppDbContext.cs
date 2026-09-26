using Microsoft.EntityFrameworkCore;
using ProductShop.Shared;

namespace ProductShop.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockEntry> StockEntries => Set<StockEntry>();
    public DbSet<StockEntryItem> StockEntryItems => Set<StockEntryItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Shob taka/amount column decimal(18,2)
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.Description).HasMaxLength(500);
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
            e.HasOne<Product>()
             .WithMany()
             .HasForeignKey(i => i.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Sale>(e =>
        {
            e.Property(s => s.InvoiceNo).IsRequired();
            e.HasMany(s => s.Items)
             .WithOne()
             .HasForeignKey(i => i.SaleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => s.SaleDate);
        });

        modelBuilder.Entity<SaleItem>(e =>
        {
            e.HasOne<Product>()
             .WithMany()
             .HasForeignKey(i => i.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
