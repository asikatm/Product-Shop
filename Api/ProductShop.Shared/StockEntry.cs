using System.ComponentModel.DataAnnotations;

namespace ProductShop.Shared;

public class StockEntry
{
    public int Id { get; set; }

    public DateTime EntryDate { get; set; } = DateTime.Today;

    [StringLength(100)]
    public string? SupplierName { get; set; }

    // Supplier er invoice / challan no
    [StringLength(50)]
    public string? ReferenceNo { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<StockEntryItem> Items { get; set; } = new();
}

public class StockEntryItem
{
    public int Id { get; set; }

    public int StockEntryId { get; set; }

    public int ProductId { get; set; }

    public int ProductVariantId { get; set; }

    [StringLength(100)]
    public string ProductName { get; set; } = string.Empty;

    // Jemon "M / Black"
    [StringLength(60)]
    public string VariantName { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public decimal UnitCost { get; set; }

    public decimal Total { get; set; }
}
