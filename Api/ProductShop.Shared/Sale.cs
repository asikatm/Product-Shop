using System.ComponentModel.DataAnnotations;

namespace ProductShop.Shared;

public class Sale
{
    public int Id { get; set; }

    // Server e toiri hoy, jemon INV-000001
    [StringLength(20)]
    public string InvoiceNo { get; set; } = string.Empty;

    public DateTime SaleDate { get; set; } = DateTime.Today;

    // Walk-in customer hole null
    public int? CustomerId { get; set; }

    [StringLength(100)]
    public string? CustomerName { get; set; }

    [StringLength(20)]
    public string? CustomerPhone { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    public decimal SubTotal { get; set; }

    public decimal Discount { get; set; }

    // Web order e delivery charge (shop sale e 0)
    public decimal DeliveryCharge { get; set; }

    // SubTotal - Discount + DeliveryCharge
    public decimal GrandTotal { get; set; }

    // Sale er somoy paid + pore joma deya baki
    public decimal PaidAmount { get; set; }

    public decimal DueAmount { get; set; }

    // Je user sale korche
    [StringLength(100)]
    public string? SoldBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<SaleItem> Items { get; set; } = new();

    // Pore joma newa baki taka
    public List<SalePayment> Payments { get; set; } = new();
}

public class SaleItem
{
    public int Id { get; set; }

    public int SaleId { get; set; }

    public int ProductId { get; set; }

    public int ProductVariantId { get; set; }

    [StringLength(100)]
    public string ProductName { get; set; } = string.Empty;

    // Jemon "M / Black"
    [StringLength(60)]
    public string VariantName { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    // Sale er somoy product er kena dam, profit hisab er jonno
    public decimal CostPrice { get; set; }

    public decimal Total { get; set; }
}

public class SalePayment
{
    public int Id { get; set; }

    public int SaleId { get; set; }

    [StringLength(20)]
    public string InvoiceNo { get; set; } = string.Empty;

    public DateTime PaymentDate { get; set; } = DateTime.Today;

    public decimal Amount { get; set; }

    [StringLength(200)]
    public string? Note { get; set; }

    [StringLength(100)]
    public string? ReceivedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
