using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Api.Services;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Permission(Perms.StockView)]
public class StockEntriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public StockEntriesController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/stockentries?from=2026-09-01&to=2026-09-30
    [HttpGet]
    public async Task<ActionResult<List<StockEntry>>> GetAll(DateTime? from, DateTime? to)
    {
        var query = _db.StockEntries.Include(s => s.Items).AsQueryable();

        if (from.HasValue)
            query = query.Where(s => s.EntryDate >= from.Value.Date);
        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            query = query.Where(s => s.EntryDate < end);
        }

        return await query
            .OrderByDescending(s => s.EntryDate)
            .ThenByDescending(s => s.Id)
            .ToListAsync();
    }

    // GET: api/stockentries/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<StockEntry>> GetById(int id)
    {
        var entry = await _db.StockEntries.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        if (entry == null) return NotFound();
        return entry;
    }

    // POST: api/stockentries
    // Save hole size/color er stock bare ar purchase price last cost e update hoy
    [HttpPost]
    [Permission(Perms.StockAdd)]
    public async Task<ActionResult<StockEntry>> Create(StockEntry entry)
    {
        if (entry.Items.Count == 0)
            return BadRequest("Kompokkhe ekta product add korun.");
        if (entry.Items.Any(i => i.Quantity <= 0))
            return BadRequest("Quantity 0 er beshi hote hobe.");
        if (entry.Items.Any(i => i.UnitCost < 0))
            return BadRequest("Unit cost negative hote parbe na.");

        var ids = entry.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        var variants = await _db.ProductVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        if (ids.Except(variants.Keys).Any())
            return BadRequest("Kichu product pawa jay nai.");

        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();
        var productNames = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);

        foreach (var item in entry.Items)
        {
            var variant = variants[item.ProductVariantId];
            item.Id = 0;
            item.ProductId = variant.ProductId;
            item.ProductName = productNames[variant.ProductId];
            item.VariantName = variant.Label;
            item.Total = item.Quantity * item.UnitCost;

            variant.Quantity += item.Quantity;
            variant.PurchasePrice = item.UnitCost;
        }

        entry.Id = 0;
        entry.EntryDate = entry.EntryDate.Date;
        entry.CreatedAt = DateTime.Now;
        entry.TotalAmount = entry.Items.Sum(i => i.Total);

        _db.StockEntries.Add(entry);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entry.Id }, entry);
    }

    // DELETE: api/stockentries/5
    // Delete hole stock abar kome jay
    [HttpDelete("{id:int}")]
    [Permission(Perms.StockDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await _db.StockEntries.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        if (entry == null) return NotFound();

        var ids = entry.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        var variants = await _db.ProductVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id);

        foreach (var group in entry.Items.GroupBy(i => i.ProductVariantId))
        {
            var variant = variants[group.Key];
            var qty = group.Sum(i => i.Quantity);
            if (variant.Quantity < qty)
                return BadRequest($"'{group.First().ProductName} ({group.First().VariantName})' er ei stock theke already sale hoye geche, tai ei entry delete kora jabe na.");

            variant.Quantity -= qty;
        }

        _db.StockEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
