namespace ProductShop.Shared;

// Dashboard er somoy (period) select
public static class StatsPeriod
{
    public const string Today = "today";
    public const string Yesterday = "yesterday";
    public const string Last7 = "7d";
    public const string Last30 = "30d";
    public const string ThisMonth = "month";

    public static readonly (string Key, string Title)[] All =
    {
        (Today, "Today"), (Yesterday, "Yesterday"), (Last7, "Last 7 days"), (Last30, "Last 30 days"), (ThisMonth, "This month")
    };
}

// Ekta KPI: ei period er value, ager same length period er value
public class KpiValue
{
    public decimal Current { get; set; }
    public decimal Previous { get; set; }

    // Koto % bere / kome geche (ager 0 hole null)
    public decimal? ChangePercent => Previous == 0 ? null : Math.Round((Current - Previous) / Math.Abs(Previous) * 100m, 1);
}

public class NamedValue
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int Count { get; set; }
}

public class TrendPoint
{
    // Jemon "10 AM" ba "28 Sep"
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
}

public class TopProduct
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Revenue { get; set; }
}

public class DueCustomer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal Due { get; set; }
}

public class DashboardStats
{
    public string Period { get; set; } = StatsPeriod.Today;
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    public KpiValue Orders { get; set; } = new();
    public KpiValue Revenue { get; set; } = new();
    public KpiValue Profit { get; set; } = new();
    public KpiValue ItemsSold { get; set; } = new();
    public KpiValue NewCustomers { get; set; } = new();
    public KpiValue Collection { get; set; } = new();

    // Kena dam dekhar permission na thakle profit lukano
    public bool CanSeeProfit { get; set; }

    public List<TrendPoint> Trend { get; set; } = new();

    // Category onujayi bikri (top 5 + Other)
    public List<NamedValue> ByCategory { get; set; } = new();

    // Paid / Partial / Due - invoice shonkha ar taka
    public List<NamedValue> ByPaymentStatus { get; set; } = new();

    public List<TopProduct> TopProducts { get; set; } = new();
    public List<DueCustomer> TopDueCustomers { get; set; } = new();

    // Header er notification
    public int LowStockCount { get; set; }
    public int PendingUsers { get; set; }
    public int PendingWebOrders { get; set; }
}
