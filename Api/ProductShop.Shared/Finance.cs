using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductShop.Shared;

// ---------- Accounts & Bank ----------

public static class AccountTypes
{
    public const string Cash = "Cash";
    public const string Bank = "Bank";
    public const string Mobile = "Mobile Banking";   // bKash, Nagad, Rocket

    public static readonly string[] All = { Cash, Bank, Mobile };

    public static string Icon(string type) => type switch
    {
        Bank => "bi-bank",
        Mobile => "bi-phone",
        _ => "bi-cash-stack"
    };
}

// Taka jekhane thake: cash box, bank account, bKash / Nagad
public class FinanceAccount
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Account er naam dite hobe")]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string Type { get; set; } = AccountTypes.Cash;

    // Bank / bKash er naam
    [StringLength(60)]
    public string? BankName { get; set; }

    [StringLength(40)]
    public string? AccountNo { get; set; }

    [StringLength(60)]
    public string? Branch { get; set; }

    // Software e shuru korar din account e koto chilo
    public decimal OpeningBalance { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(200)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Server hisab kore pathay: opening + income - expense +/- transfer
    [NotMapped]
    public decimal Balance { get; set; }
}

// ---------- Income & Expense ----------

public static class TxnTypes
{
    public const string Income = "Income";
    public const string Expense = "Expense";
    public const string Transfer = "Transfer";   // ek account theke arek account e

    public static readonly string[] All = { Income, Expense, Transfer };
}

// Kon page theke transaction ta toiri - Manual chara baki gulo nijer page theke bodlay
public static class TxnSources
{
    public const string Manual = "Manual";
    public const string Courier = "Courier";
    public const string Asset = "Asset";
}

public static class TxnCategories
{
    public static readonly string[] Income =
    {
        "Other income", "Service charge", "Commission", "Owner investment", "Loan received", "Courier COD"
    };

    public static readonly string[] Expense =
    {
        "Shop rent", "Staff salary", "Electricity bill", "Internet / Phone", "Transport", "Packaging",
        "Marketing / Boost", "Courier charge", "Repair & maintenance", "Tea / Snacks", "Asset purchase", "Owner withdraw", "Others"
    };
}

public class FinanceTransaction
{
    public int Id { get; set; }

    public DateTime Date { get; set; } = DateTime.Today;

    [StringLength(20)]
    public string Type { get; set; } = TxnTypes.Expense;

    // Income: je account e ashlo. Expense: je account theke gelo. Transfer: je account theke.
    [Range(1, int.MaxValue, ErrorMessage = "Account select korun")]
    public int AccountId { get; set; }

    // Shudhu Transfer e: je account e gelo
    public int? ToAccountId { get; set; }

    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Range(0.01, 999999999, ErrorMessage = "Taka 0 er beshi hote hobe")]
    public decimal Amount { get; set; }

    // Voucher / cheque / trx id
    [StringLength(50)]
    public string? Reference { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }

    [StringLength(20)]
    public string Source { get; set; } = TxnSources.Manual;

    public int? CourierSettlementId { get; set; }
    public int? AssetId { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [NotMapped]
    public string? AccountName { get; set; }

    [NotMapped]
    public string? ToAccountName { get; set; }
}

// ---------- Courier Settlement ----------

public static class Couriers
{
    public static readonly string[] All = { "Steadfast", "Pathao", "RedX", "Paperfly", "Sundarban", "eCourier", "Own delivery" };
}

// Courier company COD er taka kete niye baki ta account e pathay
public class CourierSettlement
{
    public int Id { get; set; }

    public DateTime Date { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Courier er naam dite hobe")]
    [StringLength(50)]
    public string CourierName { get; set; } = string.Empty;

    // Je account e taka ashlo
    [Range(1, int.MaxValue, ErrorMessage = "Account select korun")]
    public int AccountId { get; set; }

    // Courier er invoice / payment id
    [StringLength(50)]
    public string? ReferenceNo { get; set; }

    public int OrderCount { get; set; }

    // Customer der kach theke courier je taka tuleche
    public decimal CodAmount { get; set; }

    // Delivery charge + COD charge (courier kete rakhe)
    public decimal DeliveryCharge { get; set; }
    public decimal CodCharge { get; set; }

    // Account e je taka elo
    public decimal NetAmount { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [NotMapped]
    public string? AccountName { get; set; }

    // Ei settlement e kon web order gulo
    [NotMapped]
    public List<string> OrderNos { get; set; } = new();
}

public class CourierSettlementRequest
{
    public DateTime Date { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Courier select korun")]
    [StringLength(50)]
    public string CourierName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Account select korun")]
    public int AccountId { get; set; }

    [StringLength(50)]
    public string? ReferenceNo { get; set; }

    // Deliver hoye geche emon web order gulo (optional)
    public List<int> WebOrderIds { get; set; } = new();

    // Order select na korle nije likhe dite hoy
    public int OrderCount { get; set; }

    [Range(0, 999999999, ErrorMessage = "COD amount thik nai")]
    public decimal CodAmount { get; set; }

    [Range(0, 999999999, ErrorMessage = "Delivery charge thik nai")]
    public decimal DeliveryCharge { get; set; }

    [Range(0, 999999999, ErrorMessage = "COD charge thik nai")]
    public decimal CodCharge { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }
}

// Settle kora baki: deliver hoyeche kintu courier theke taka ashe nai
public class PendingCourierOrder
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal DeliveryCharge { get; set; }
    public DateTime DeliveredAt { get; set; }
}

// ---------- Asset Management ----------

public static class AssetStatus
{
    public const string Active = "Active";
    public const string Repair = "Repair";
    public const string Disposed = "Disposed";

    public static readonly string[] All = { Active, Repair, Disposed };
}

public static class AssetCategories
{
    public static readonly string[] All = { "Furniture", "Computer / Electronics", "Display rack / Mannequin", "Machinery", "Vehicle", "Decoration", "Others" };
}

public class Asset
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Asset er naam dite hobe")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string Category { get; set; } = "Furniture";

    public DateTime PurchaseDate { get; set; } = DateTime.Today;

    [Range(0, 999999999, ErrorMessage = "Dam thik nai")]
    public decimal PurchasePrice { get; set; }

    [Range(1, 100000, ErrorMessage = "Quantity kompokkhe 1")]
    public int Quantity { get; set; } = 1;

    // Bochore koto % dam kome (0 = kome na)
    [Range(0, 100, ErrorMessage = "0 - 100 %")]
    public decimal DepreciationPercent { get; set; }

    [StringLength(100)]
    public string? Location { get; set; }

    [StringLength(100)]
    public string? Supplier { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = AssetStatus.Active;

    // Kon account theke taka deya hoyeche - dile Expense e entry hoy
    public int? AccountId { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public decimal TotalCost => PurchasePrice * Quantity;

    // Straight-line: protibochor TotalCost er DepreciationPercent % kome, 0 er niche na
    public decimal CurrentValue
    {
        get
        {
            if (Status == AssetStatus.Disposed) return 0;
            var years = (decimal)Math.Max(0, (DateTime.Today - PurchaseDate.Date).TotalDays) / 365m;
            var value = TotalCost * (1 - DepreciationPercent / 100m * years);
            return Math.Round(Math.Max(0, value), 2);
        }
    }
}

// ---------- Report ----------

public class FinanceSummary
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    // Shob active account er mot balance (ajker)
    public decimal TotalBalance { get; set; }

    // Ei somoyer
    public decimal SalesCollection { get; set; }
    public decimal OtherIncome { get; set; }
    public decimal Expense { get; set; }
    public decimal StockPurchase { get; set; }
    public decimal NetCashFlow => SalesCollection + OtherIncome - Expense - StockPurchase;

    // Kena dam dekhar permission thakle
    public decimal? GrossProfit { get; set; }
    public decimal? NetProfit => GrossProfit == null ? null : GrossProfit + OtherIncome - Expense;

    public decimal CourierPendingAmount { get; set; }
    public int CourierPendingOrders { get; set; }
    public decimal AssetValue { get; set; }

    public List<NamedValue> AccountBalances { get; set; } = new();
    public List<NamedValue> ExpenseByCategory { get; set; } = new();
    // Din (ba 2 mash er beshi hole mash) onujayi cash in / out
    public List<CashFlowRow> Trend { get; set; } = new();
    public List<FinanceTransaction> Recent { get; set; } = new();
}

public class CashFlowRow
{
    public string Label { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal SalesCollection { get; set; }
    public decimal OtherIncome { get; set; }
    public decimal Expense { get; set; }
    public decimal StockPurchase { get; set; }

    public decimal CashIn => SalesCollection + OtherIncome;
    public decimal CashOut => Expense + StockPurchase;
    public decimal Net => CashIn - CashOut;

    // Period er shuru theke jog kore
    public decimal Running { get; set; }
}

public class CashFlowReport
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string GroupBy { get; set; } = "day";
    public List<CashFlowRow> Rows { get; set; } = new();
}
