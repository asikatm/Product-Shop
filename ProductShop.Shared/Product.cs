using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductShop.Shared;

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Product name is required")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    // Style / design code, jemon "TS-101"
    [StringLength(30)]
    public string? Code { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Protiti size + color er alada stock ar dam
    public List<ProductVariant> Variants { get; set; } = new();

    [NotMapped]
    public int TotalStock => Variants.Sum(v => v.Quantity);
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
