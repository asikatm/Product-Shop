using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace ProductShop.Shared;

public static class CustomerTypes
{
    public const string Regular = "Regular";
    public const string Vip = "VIP";
    public const string Wholesale = "Wholesale";

    public static readonly string[] All = { Regular, Vip, Wholesale };
}

public static class CustomerOptions
{
    public static readonly string[] Genders = { "Male", "Female", "Other" };
}

// Bangladesh er mobile number: "+880 1711-000000", "8801711000000", "1711000000" shob "01711000000" hoye jay
public static partial class PhoneHelper
{
    public static string? Normalize(string? input)
    {
        var digits = new string((input ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 13 && digits.StartsWith("880")) digits = digits[2..];
        if (digits.Length == 10 && digits.StartsWith('1')) digits = "0" + digits;
        return BdMobile().IsMatch(digits) ? digits : null;
    }

    // "01711000000" -> "01711-000000"
    public static string Pretty(string? phone) =>
        phone is { Length: 11 } ? $"{phone[..5]}-{phone[5..]}" : phone ?? string.Empty;

    [GeneratedRegex("^01[3-9][0-9]{8}$")]
    private static partial Regex BdMobile();
}

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

    [StringLength(20)]
    public string? AltPhone { get; set; }

    [EmailAddress(ErrorMessage = "Email thik nai")]
    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(10)]
    public string? Gender { get; set; }

    // Jonmodin e offer / SMS pathanor jonno
    public DateTime? DateOfBirth { get; set; }

    [StringLength(20)]
    public string CustomerType { get; set; } = CustomerTypes.Regular;

    [StringLength(200)]
    public string? Address { get; set; }

    // Area / city, jemon "Mirpur 10, Dhaka"
    [StringLength(60)]
    public string? City { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

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
