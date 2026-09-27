using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductShop.Shared;

public static class ProductStatus
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    public static readonly string[] All = { Draft, Active, Inactive };
}

// Form er dropdown gulor list
public static class ProductOptions
{
    public static readonly string[] Genders = { "Men", "Women", "Boys", "Girls", "Kids", "Unisex" };
    public static readonly string[] Fabrics = { "Cotton", "Polyester", "Linen", "Denim", "Silk", "Viscose", "Georgette", "Khadi", "Blended", "Other" };
    public static readonly string[] FitTypes = { "Regular", "Slim", "Loose", "Oversized" };
    public static readonly string[] Sleeves = { "Full", "Half", "3/4", "Sleeveless" };
    public static readonly string[] Seasons = { "All season", "Summer", "Winter", "Eid collection", "Puja collection", "Boishakh" };
    public static readonly string[] Sizes = { "XS", "S", "M", "L", "XL", "XXL", "3XL" };
}

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Product name is required")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    // Style / design code, jemon "TS-101"
    [StringLength(30)]
    public string? Code { get; set; }

    [StringLength(40)]
    public string? Barcode { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }

    [StringLength(20)]
    public string? Gender { get; set; }

    [StringLength(30)]
    public string? Fabric { get; set; }

    [StringLength(20)]
    public string? FitType { get; set; }

    [StringLength(20)]
    public string? Sleeve { get; set; }

    [StringLength(30)]
    public string? Season { get; set; }

    // Notun variant er default kena / bikri dam
    [Range(0, 10000000, ErrorMessage = "Purchase price must be 0 or more")]
    public decimal PurchasePrice { get; set; }

    [Range(0, 10000000, ErrorMessage = "Selling price must be 0 or more")]
    public decimal Price { get; set; }

    [Range(0, 10000000, ErrorMessage = "Discount must be 0 or more")]
    public decimal Discount { get; set; }

    // true hole Discount %, na hole taka
    public bool DiscountIsPercent { get; set; } = true;

    [Range(0, 100, ErrorMessage = "VAT must be 0-100")]
    public decimal VatPercent { get; set; }

    public int? ShopId { get; set; }
    public Shop? Shop { get; set; }

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = ProductStatus.Active;

    // Comma diye, jemon "summer, cotton"
    [StringLength(200)]
    public string? Tags { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Protiti size + color er alada stock ar dam
    public List<ProductVariant> Variants { get; set; } = new();

    // Prothom ta main image
    public List<ProductImage> Images { get; set; } = new();

    [NotMapped]
    public int TotalStock => Variants.Sum(v => v.Quantity);

    // Discount er pore dam (VAT chara)
    [NotMapped]
    public decimal FinalPrice => PriceAfterDiscount(Price);

    public decimal PriceAfterDiscount(decimal price)
    {
        var off = DiscountIsPercent ? price * Discount / 100m : Discount;
        return Math.Max(0, Math.Round(price - off, 2));
    }
}

public class ProductVariant
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    [Required(ErrorMessage = "Size is required")]
    [StringLength(20)]
    public string Size { get; set; } = "Free";

    [StringLength(30)]
    public string Color { get; set; } = string.Empty;

    // Barcode / SKU. Khali rakhle server nije banabe.
    [StringLength(40)]
    public string? Sku { get; set; }

    // Kena dam (last stock entry theke update hoy)
    [Range(0, 10000000, ErrorMessage = "Purchase price must be 0 or more")]
    public decimal PurchasePrice { get; set; }

    // Bikri dam
    [Range(0, 10000000, ErrorMessage = "Sale price must be 0 or more")]
    public decimal Price { get; set; }

    // Current stock. Stock In e bare, Sale e kome.
    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be 0 or more")]
    public int Quantity { get; set; }

    // Stock eto ba er kom hole "Low stock" dekhabe
    [Range(0, int.MaxValue, ErrorMessage = "Reorder level must be 0 or more")]
    public int ReorderLevel { get; set; } = 3;

    [NotMapped]
    public string Label => string.IsNullOrWhiteSpace(Color) ? Size : $"{Size} / {Color}";
}

public class ProductImage
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    // API er relative path, jemon "uploads/products/abc.jpg"
    [StringLength(200)]
    public string Url { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
