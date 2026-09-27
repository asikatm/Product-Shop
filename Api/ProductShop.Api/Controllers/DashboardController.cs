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
    // Profit ar stock value shudhu admin pay
    [HttpGet]
    public async Task<ActionResult<DashboardSummary>> Get()
    {
        var isAdmin = User.IsInRole(Roles.Admin);
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var todaySales = _db.Sales.Where(s => s.SaleDate >= today && s.SaleDate < tomorrow);
        var monthSales = _db.Sales.Where(s => s.SaleDate >= monthStart && s.SaleDate < tomorrow);
        var lowStock = _db.ProductVariants.Where(v => v.Quantity <= v.ReorderLevel);

        return new DashboardSummary
        {
            TotalProducts = await _db.Products.CountAsync(),
            TotalVariants = await _db.ProductVariants.CountAsync(),
            TotalStockQty = await _db.ProductVariants.SumAsync(v => v.Quantity),
            StockValue = isAdmin ? await _db.ProductVariants.SumAsync(v => v.Quantity * v.PurchasePrice) : 0,
            LowStockCount = await lowStock.CountAsync(),

            TodayInvoices = await todaySales.CountAsync(),
            TodaySales = await todaySales.SumAsync(s => s.GrandTotal),
            TodayProfit = isAdmin ? await ProfitAsync(todaySales) : 0,
            TodayCollection = await _db.SalePayments.Where(p => p.PaymentDate >= today && p.PaymentDate < tomorrow).SumAsync(p => p.Amount),

            MonthSales = await monthSales.SumAsync(s => s.GrandTotal),
            MonthProfit = isAdmin ? await ProfitAsync(monthSales) : 0,

            TotalDue = await _db.Sales.SumAsync(s => s.DueAmount),
            TotalCustomers = await _db.Customers.CountAsync(),

            LowStockItems = await lowStock
                .OrderBy(v => v.Quantity)
                .Take(15)
                .Join(_db.Products, v => v.ProductId, p => p.Id, (v, p) => new LowStockItem
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    VariantName = v.Color == "" ? v.Size : v.Size + " / " + v.Color,
                    Quantity = v.Quantity,
                    ReorderLevel = v.ReorderLevel
                })
                .ToListAsync(),
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
