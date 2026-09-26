using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductShop.Shared;

public class Customer
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Customer name is required")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    // Phone diye customer chena hoy, tai unique
    [Required(ErrorMessage = "Phone number is required")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // API hisab kore pathay, database e thake na
    [NotMapped]
    public decimal TotalPurchase { get; set; }

    [NotMapped]
    public decimal TotalDue { get; set; }
}

public class CustomerDetails
{
    public Customer Customer { get; set; } = new();
    public List<Sale> Sales { get; set; } = new();
    public List<SalePayment> Payments { get; set; } = new();
}

public class ReceivePaymentRequest
{
    [Range(0.01, 100000000, ErrorMessage = "Amount 0 er beshi hote hobe")]
    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.Today;

    [StringLength(200)]
    public string? Note { get; set; }
}
