using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Api.Services;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesController : ControllerBase
{
    private readonly AppDbContext _db;

    public SalesController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/sales?from=2026-09-01&to=2026-09-30
    [HttpGet]
    [Permission(Perms.SalesView)]
    public async Task<ActionResult<List<Sale>>> GetAll(DateTime? from, DateTime? to)
    {
        var query = _db.Sales.AsNoTracking().Include(s => s.Items).AsQueryable();

        if (from.HasValue)
            query = query.Where(s => s.SaleDate >= from.Value.Date);
        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            query = query.Where(s => s.SaleDate < end);
        }

        var sales = await query
            .OrderByDescending(s => s.SaleDate)
            .ThenByDescending(s => s.Id)
            .ToListAsync();
        await HideCostAsync(sales);
        return sales;
    }

    // GET: api/sales/5
    [HttpGet("{id:int}")]
    [Permission(Perms.SalesView, Perms.SalesAdd)]
    public async Task<ActionResult<Sale>> GetById(int id)
    {
        var sale = await _db.Sales.AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.Payments.OrderBy(p => p.PaymentDate).ThenBy(p => p.Id))
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return NotFound();
        await HideCostAsync(new[] { sale });
        return sale;
    }

    // POST: api/sales
    // Save hole size/color er stock kome jay
    [HttpPost]
    [Permission(Perms.SalesAdd)]
    public async Task<ActionResult<Sale>> Create(Sale sale)
    {
        var (created, error) = await HttpContext.RequestServices.GetRequiredService<SaleWriter>().CreateAsync(sale, User.DisplayName());
        if (error != null) return BadRequest(error);
        return CreatedAtAction(nameof(GetById), new { id = created!.Id }, created);
    }

    // DELETE: api/sales/5
    // Delete hole stock ferot ashe
    [HttpDelete("{id:int}")]
    [Permission(Perms.SalesDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return NotFound();

        await HttpContext.RequestServices.GetRequiredService<SaleWriter>().DeleteAsync(sale);
        return NoContent();
    }

    // Kena dam shudhu admin dekhbe
    private async Task HideCostAsync(IEnumerable<Sale> sales)
    {
        if (await HttpContext.RequestServices.GetRequiredService<PermissionService>().HasAsync(User, Perms.CostView)) return;
        foreach (var item in sales.SelectMany(s => s.Items))
            item.CostPrice = 0;
    }
}
