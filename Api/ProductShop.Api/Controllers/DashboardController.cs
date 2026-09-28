using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Api.Services;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PermissionService _perms;

    public DashboardController(AppDbContext db, PermissionService perms)
    {
        _db = db;
        _perms = perms;
    }

    // GET: api/dashboard/stats?period=7d
    // Period er KPI (ager same length period er sathe tulona), chart er data
    [HttpGet("stats")]
    [Permission(Perms.DashboardView)]
    public async Task<ActionResult<DashboardStats>> Stats(string? period)
    {
        period ??= StatsPeriod.Today;
        var (from, to) = Range(period);
        if (from == default) return BadRequest("Period thik nai.");
        var length = to - from;
        var prevFrom = from - length;

        var canSeeProfit = await _perms.HasAsync(User, Perms.CostView);
        var cur = _db.Sales.Where(s => s.SaleDate >= from && s.SaleDate < to);
        var prev = _db.Sales.Where(s => s.SaleDate >= prevFrom && s.SaleDate < from);
        var curItems = _db.SaleItems.Where(i => cur.Any(s => s.Id == i.SaleId));
        var prevItems = _db.SaleItems.Where(i => prev.Any(s => s.Id == i.SaleId));

        var stats = new DashboardStats
        {
            Period = period,
            From = from,
            To = to,
            CanSeeProfit = canSeeProfit,
            Orders = new() { Current = await cur.CountAsync(), Previous = await prev.CountAsync() },
            Revenue = new() { Current = await cur.SumAsync(s => s.GrandTotal), Previous = await prev.SumAsync(s => s.GrandTotal) },
            ItemsSold = new() { Current = await curItems.SumAsync(i => i.Quantity), Previous = await prevItems.SumAsync(i => i.Quantity) },
            NewCustomers = new()
            {
                Current = await _db.Customers.CountAsync(c => c.CreatedAt >= from && c.CreatedAt < to),
                Previous = await _db.Customers.CountAsync(c => c.CreatedAt >= prevFrom && c.CreatedAt < from)
            },
            // Sale er somoy paid + pore baki joma
            Collection = new()
            {
                Current = await CollectionAsync(from, to),
                Previous = await CollectionAsync(prevFrom, from)
            },
            LowStockCount = await _db.ProductVariants.CountAsync(v => v.Quantity <= v.ReorderLevel)
        };

        if (canSeeProfit)
            stats.Profit = new() { Current = await ProfitAsync(cur), Previous = await ProfitAsync(prev) };

        if (await _perms.HasAsync(User, Perms.WebOrdersView))
            stats.PendingWebOrders = await _db.WebOrders.CountAsync(o => o.Status == WebOrderStatus.Pending);

        if (await _perms.HasAsync(User, Perms.UsersView))
            stats.PendingUsers = await _db.Users.CountAsync(u => !u.IsActive && u.LastLoginAt == null);

        // Trend: ek diner period e ghonta onujayi, na hole din onujayi
        var sales = await cur.Select(s => new { s.SaleDate, s.CreatedAt, s.GrandTotal }).ToListAsync();
        if (length.TotalDays <= 1)
        {
            var from8 = 8;
            var lastHour = Math.Max(21, sales.Count == 0 ? 0 : sales.Max(s => s.CreatedAt.Hour));
            var firstHour = Math.Min(from8, sales.Count == 0 ? from8 : sales.Min(s => s.CreatedAt.Hour));
            for (var h = firstHour; h <= lastHour; h++)
            {
                var inHour = sales.Where(s => s.CreatedAt.Hour == h).ToList();
                stats.Trend.Add(new TrendPoint { Label = DateTime.Today.AddHours(h).ToString("h tt"), Revenue = inHour.Sum(s => s.GrandTotal), Orders = inHour.Count });
            }
        }
        else
        {
            for (var d = from; d < to; d = d.AddDays(1))
            {
                var inDay = sales.Where(s => s.SaleDate.Date == d).ToList();
                stats.Trend.Add(new TrendPoint { Label = d.ToString("dd MMM"), Revenue = inDay.Sum(s => s.GrandTotal), Orders = inDay.Count });
            }
        }

        // Category onujayi (top 5, baki "Other")
        var byCategory = await curItems
            .Join(_db.Products, i => i.ProductId, p => p.Id, (i, p) => new { p.CategoryId, i.Total, i.Quantity })
            .GroupBy(x => x.CategoryId)
            .Select(g => new { CategoryId = g.Key, Revenue = g.Sum(x => x.Total), Qty = g.Sum(x => x.Quantity) })
            .ToListAsync();
        var categoryNames = await _db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name);
        var ranked = byCategory
            .Select(c => new NamedValue
            {
                Name = c.CategoryId is int id && categoryNames.TryGetValue(id, out var n) ? n : "No category",
                Value = c.Revenue,
                Count = c.Qty
            })
            .OrderByDescending(c => c.Value)
            .ToList();
        stats.ByCategory = ranked.Take(5).ToList();
        if (ranked.Count > 5)
            stats.ByCategory.Add(new NamedValue { Name = "Other", Value = ranked.Skip(5).Sum(c => c.Value), Count = ranked.Skip(5).Sum(c => c.Count) });

        // Payment status
        var pay = await cur.Select(s => new { s.GrandTotal, s.PaidAmount, s.DueAmount }).ToListAsync();
        stats.ByPaymentStatus = new()
        {
            new() { Name = "Paid", Count = pay.Count(p => p.DueAmount == 0), Value = pay.Where(p => p.DueAmount == 0).Sum(p => p.GrandTotal) },
            new() { Name = "Partial", Count = pay.Count(p => p.DueAmount > 0 && p.PaidAmount > 0), Value = pay.Where(p => p.DueAmount > 0 && p.PaidAmount > 0).Sum(p => p.GrandTotal) },
            new() { Name = "Due", Count = pay.Count(p => p.DueAmount > 0 && p.PaidAmount == 0), Value = pay.Where(p => p.DueAmount > 0 && p.PaidAmount == 0).Sum(p => p.GrandTotal) }
        };

        stats.TopProducts = await curItems
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new TopProduct { ProductId = g.Key.ProductId, Name = g.Key.ProductName, Quantity = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.Total) })
            .OrderByDescending(p => p.Quantity)
            .Take(5)
            .ToListAsync();

        stats.TopDueCustomers = await _db.Sales
            .Where(s => s.CustomerId != null && s.DueAmount > 0)
            .GroupBy(s => new { s.CustomerId, s.CustomerName, s.CustomerPhone })
            .Select(g => new DueCustomer { Id = g.Key.CustomerId!.Value, Name = g.Key.CustomerName ?? "", Phone = g.Key.CustomerPhone ?? "", Due = g.Sum(s => s.DueAmount) })
            .OrderByDescending(c => c.Due)
            .Take(5)
            .ToListAsync();

        return stats;
    }

    private static (DateTime From, DateTime To) Range(string period)
    {
        var today = DateTime.Today;
        return period switch
        {
            StatsPeriod.Today => (today, today.AddDays(1)),
            StatsPeriod.Yesterday => (today.AddDays(-1), today),
            StatsPeriod.Last7 => (today.AddDays(-6), today.AddDays(1)),
            StatsPeriod.Last30 => (today.AddDays(-29), today.AddDays(1)),
            StatsPeriod.ThisMonth => (new DateTime(today.Year, today.Month, 1), today.AddDays(1)),
            _ => (default, default)
        };
    }

    private async Task<decimal> CollectionAsync(DateTime from, DateTime to)
    {
        // Sale er shomoy paid = total paid - pore joma (payment table)
        var sales = _db.Sales.Where(s => s.SaleDate >= from && s.SaleDate < to);
        var paidOnSale = await sales.SumAsync(s => s.PaidAmount) - await _db.SalePayments.Where(p => sales.Any(s => s.Id == p.SaleId)).SumAsync(p => p.Amount);
        var dueCollected = await _db.SalePayments.Where(p => p.PaymentDate >= from && p.PaymentDate < to).SumAsync(p => p.Amount);
        return paidOnSale + dueCollected;
    }

    // GET: api/dashboard
    // Profit ar stock value shudhu admin pay
    [HttpGet]
    [Permission(Perms.DashboardView)]
    public async Task<ActionResult<DashboardSummary>> Get()
    {
        var isAdmin = await _perms.HasAsync(User, Perms.CostView);
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
