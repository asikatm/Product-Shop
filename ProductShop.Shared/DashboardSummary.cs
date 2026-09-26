namespace ProductShop.Shared;

public class DashboardSummary
{
    public int TotalProducts { get; set; }
    public int TotalVariants { get; set; }
    public int TotalStockQty { get; set; }
    public decimal StockValue { get; set; }
    public int LowStockCount { get; set; }

    public int TodayInvoices { get; set; }
    public decimal TodaySales { get; set; }
    public decimal TodayProfit { get; set; }
    public decimal TodayCollection { get; set; }

    public decimal MonthSales { get; set; }
    public decimal MonthProfit { get; set; }

    public decimal TotalDue { get; set; }
    public int TotalCustomers { get; set; }

    public List<LowStockItem> LowStockItems { get; set; } = new();
    public List<Sale> RecentSales { get; set; } = new();
}

public class LowStockItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int ReorderLevel { get; set; }
}
