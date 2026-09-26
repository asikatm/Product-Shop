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

    [Range(0, 10000000, ErrorMessage = "Price must be 0 or more")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be 0 or more")]
    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
