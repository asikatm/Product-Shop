namespace ProductShop.Shared;

public class DashboardSummary
{
    public int TotalProducts { get; set; }
    public int TotalStockQty { get; set; }
    public decimal StockValue { get; set; }
    public int LowStockCount { get; set; }

    public int TodayInvoices { get; set; }
    public decimal TodaySales { get; set; }
    public decimal TodayProfit { get; set; }

    public decimal MonthSales { get; set; }
    public decimal MonthProfit { get; set; }

    public decimal TotalDue { get; set; }

    public List<Product> LowStockProducts { get; set; } = new();
    public List<Sale> RecentSales { get; set; } = new();
}
