using Microsoft.EntityFrameworkCore;
using ProductShop.Shared;

namespace ProductShop.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<StockEntry> StockEntries => Set<StockEntry>();
    public DbSet<StockEntryItem> StockEntryItems => Set<StockEntryItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> AppRoles => Set<AppRole>();
    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<WebOrder> WebOrders => Set<WebOrder>();
    public DbSet<WebOrderItem> WebOrderItems => Set<WebOrderItem>();
    public DbSet<FinanceAccount> FinanceAccounts => Set<FinanceAccount>();
    public DbSet<FinanceTransaction> FinanceTransactions => Set<FinanceTransaction>();
    public DbSet<CourierSettlement> CourierSettlements => Set<CourierSettlement>();
    public DbSet<Asset> Assets => Set<Asset>();

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

        modelBuilder.Entity<Shop>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(s => s.Name).IsUnique();
        });

        modelBuilder.Entity<Supplier>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(s => s.Name).IsUnique();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.Code).HasMaxLength(30);
            e.Property(p => p.Barcode).HasMaxLength(40);
            e.Property(p => p.Description).HasMaxLength(500);
            e.Property(p => p.Gender).HasMaxLength(20);
            e.Property(p => p.Fabric).HasMaxLength(30);
            e.Property(p => p.FitType).HasMaxLength(20);
            e.Property(p => p.Sleeve).HasMaxLength(20);
            e.Property(p => p.Season).HasMaxLength(30);
            e.Property(p => p.Tags).HasMaxLength(200);
            // Purono product gulo Active thakbe
            e.Property(p => p.Status).HasMaxLength(20).IsRequired().HasDefaultValue(ProductStatus.Active);            e.HasIndex(p => p.Code);

            // Je category / brand / shop / supplier e product ache, take delete kora jabe na
            e.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Brand).WithMany().HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Shop).WithMany().HasForeignKey(p => p.ShopId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);

            e.HasMany(p => p.Variants)
             .WithOne()
             .HasForeignKey(v => v.ProductId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(p => p.Images)
             .WithOne()
             .HasForeignKey(i => i.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductImage>(e =>
        {
            e.Property(i => i.Url).HasMaxLength(200).IsRequired();
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
            // Purono customer ra "Regular" hobe
            e.Property(c => c.CustomerType).HasMaxLength(20).IsRequired().HasDefaultValue(CustomerTypes.Regular);
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
            e.Property(u => u.Email).HasMaxLength(100);
            e.Property(u => u.Phone).HasMaxLength(20);
            e.HasIndex(u => u.Username).IsUnique();
            e.HasMany(u => u.UserRoles).WithOne().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WebOrder>(e =>
        {
            e.Property(o => o.OrderNo).IsRequired();
            e.HasIndex(o => o.OrderNo).IsUnique();
            e.HasIndex(o => o.Status);
            e.HasIndex(o => o.CreatedAt);
            e.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.WebOrderId).OnDelete(DeleteBehavior.Cascade);
            // Sale delete hole order theke link shoriye dey
            e.HasOne<Sale>().WithMany().HasForeignKey(o => o.SaleId).OnDelete(DeleteBehavior.SetNull);
            // Settlement delete hole order abar "settle baki" hoy
            e.HasOne<CourierSettlement>().WithMany().HasForeignKey(o => o.CourierSettlementId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FinanceAccount>(e =>
        {
            e.HasIndex(a => a.Name).IsUnique();
        });

        modelBuilder.Entity<FinanceTransaction>(e =>
        {
            e.HasIndex(t => t.Date);
            // Transaction thakle account delete kora jabe na
            e.HasOne<FinanceAccount>().WithMany().HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinanceAccount>().WithMany().HasForeignKey(t => t.ToAccountId).OnDelete(DeleteBehavior.Restrict);
            // Settlement / asset delete hole tar transaction o jay
            e.HasOne<CourierSettlement>().WithMany().HasForeignKey(t => t.CourierSettlementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Asset>().WithMany().HasForeignKey(t => t.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CourierSettlement>(e =>
        {
            e.HasIndex(s => s.Date);
            e.HasOne<FinanceAccount>().WithMany().HasForeignKey(s => s.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Asset>(e =>
        {
            e.HasOne<FinanceAccount>().WithMany().HasForeignKey(a => a.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WebOrderItem>(e =>
        {
            e.HasOne<ProductVariant>().WithMany().HasForeignKey(i => i.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppRole>(e =>
        {
            e.ToTable("Roles");
            e.Property(r => r.Name).HasMaxLength(50).IsRequired();
            e.Property(r => r.Description).HasMaxLength(200);
            e.HasIndex(r => r.Name).IsUnique();
            e.HasMany(r => r.Permissions).WithOne().HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.UserRoles).WithOne(ur => ur.Role).HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppUserRole>(e =>
        {
            e.ToTable("UserRoles");
            e.HasKey(ur => new { ur.UserId, ur.RoleId });
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.ToTable("RolePermissions");
            e.HasKey(p => new { p.RoleId, p.Permission });
            e.Property(p => p.Permission).HasMaxLength(60);
        });
    }
}
