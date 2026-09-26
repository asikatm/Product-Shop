using System.ComponentModel.DataAnnotations;

namespace ProductShop.Shared;

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Product name is required")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

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
    public int ReorderLevel { get; set; } = 5;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
