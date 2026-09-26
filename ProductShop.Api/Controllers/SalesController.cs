using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
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
    public async Task<ActionResult<List<Sale>>> GetAll(DateTime? from, DateTime? to)
    {
        var query = _db.Sales.Include(s => s.Items).AsQueryable();

        if (from.HasValue)
            query = query.Where(s => s.SaleDate >= from.Value.Date);
        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            query = query.Where(s => s.SaleDate < end);
        }

        return await query
            .OrderByDescending(s => s.SaleDate)
            .ThenByDescending(s => s.Id)
            .ToListAsync();
    }

    // GET: api/sales/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Sale>> GetById(int id)
    {
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return NotFound();
        return sale;
    }

    // POST: api/sales
    // Save hole product er stock kome jay
    [HttpPost]
    public async Task<ActionResult<Sale>> Create(Sale sale)
    {
        if (sale.Items.Count == 0)
            return BadRequest("Kompokkhe ekta product add korun.");
        if (sale.Items.Any(i => i.Quantity <= 0))
            return BadRequest("Quantity 0 er beshi hote hobe.");
        if (sale.Items.Any(i => i.UnitPrice < 0))
            return BadRequest("Unit price negative hote parbe na.");

        var ids = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        if (ids.Except(products.Keys).Any())
            return BadRequest("Kichu product pawa jay nai.");

        foreach (var group in sale.Items.GroupBy(i => i.ProductId))
        {
            var product = products[group.Key];
            var need = group.Sum(i => i.Quantity);
            if (product.Quantity < need)
                return BadRequest($"'{product.Name}' er stock ache {product.Quantity}, kintu chawa hoyeche {need}.");
        }

        foreach (var item in sale.Items)
        {
            var product = products[item.ProductId];
            item.Id = 0;
            item.ProductName = product.Name;
            item.CostPrice = product.PurchasePrice;
            item.Total = item.Quantity * item.UnitPrice;

            product.Quantity -= item.Quantity;
        }

        sale.SubTotal = sale.Items.Sum(i => i.Total);
        if (sale.Discount < 0 || sale.Discount > sale.SubTotal)
            return BadRequest("Discount 0 theke sub total er moddhe hote hobe.");
        if (sale.PaidAmount < 0)
            return BadRequest("Paid amount negative hote parbe na.");

        sale.Id = 0;
        sale.SaleDate = sale.SaleDate.Date;
        sale.CreatedAt = DateTime.Now;
        sale.GrandTotal = sale.SubTotal - sale.Discount;
        sale.PaidAmount = Math.Min(sale.PaidAmount, sale.GrandTotal);
        sale.DueAmount = sale.GrandTotal - sale.PaidAmount;
        sale.InvoiceNo = "TEMP";

        // Invoice no er jonno Id lage, tai duibar save; transaction e rakha holo
        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Sales.Add(sale);
        await _db.SaveChangesAsync();
        sale.InvoiceNo = $"INV-{sale.Id:D6}";
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return CreatedAtAction(nameof(GetById), new { id = sale.Id }, sale);
    }

    // DELETE: api/sales/5
    // Delete hole stock ferot ashe
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return NotFound();

        var ids = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        foreach (var item in sale.Items)
            products[item.ProductId].Quantity += item.Quantity;

        _db.Sales.Remove(sale);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
