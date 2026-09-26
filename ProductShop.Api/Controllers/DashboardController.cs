using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/dashboard
    [HttpGet]
    public async Task<ActionResult<DashboardSummary>> Get()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var todaySales = _db.Sales.Where(s => s.SaleDate >= today && s.SaleDate < tomorrow);
        var monthSales = _db.Sales.Where(s => s.SaleDate >= monthStart && s.SaleDate < tomorrow);
        var lowStock = _db.Products.Where(p => p.Quantity <= p.ReorderLevel);

        return new DashboardSummary
        {
            TotalProducts = await _db.Products.CountAsync(),
            TotalStockQty = await _db.Products.SumAsync(p => p.Quantity),
            StockValue = await _db.Products.SumAsync(p => p.Quantity * p.PurchasePrice),
            LowStockCount = await lowStock.CountAsync(),

            TodayInvoices = await todaySales.CountAsync(),
            TodaySales = await todaySales.SumAsync(s => s.GrandTotal),
            TodayProfit = await ProfitAsync(todaySales),

            MonthSales = await monthSales.SumAsync(s => s.GrandTotal),
            MonthProfit = await ProfitAsync(monthSales),

            TotalDue = await _db.Sales.SumAsync(s => s.DueAmount),

            LowStockProducts = await lowStock.OrderBy(p => p.Quantity).Take(10).ToListAsync(),
            RecentSales = await _db.Sales.OrderByDescending(s => s.Id).Take(10).ToListAsync()
        };
    }

    // Profit = (bikri dam - kena dam) * qty - discount
    private async Task<decimal> ProfitAsync(IQueryable<Sale> sales)
    {
        var gross = await _db.SaleItems
            .Where(i => sales.Any(s => s.Id == i.SaleId))
            .SumAsync(i => (i.UnitPrice - i.CostPrice) * i.Quantity);
        var discount = await sales.SumAsync(s => s.Discount);
        return gross - discount;
    }
}
