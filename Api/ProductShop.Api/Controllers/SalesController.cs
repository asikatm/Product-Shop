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
        HideCost(sales);
        return sales;
    }

    // GET: api/sales/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Sale>> GetById(int id)
    {
        var sale = await _db.Sales.AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.Payments.OrderBy(p => p.PaymentDate).ThenBy(p => p.Id))
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return NotFound();
        HideCost(new[] { sale });
        return sale;
    }

    // POST: api/sales
    // Save hole size/color er stock kome jay
    [HttpPost]
    public async Task<ActionResult<Sale>> Create(Sale sale)
    {
        if (sale.Items.Count == 0)
            return BadRequest("Kompokkhe ekta product add korun.");
        if (sale.Items.Any(i => i.Quantity <= 0))
            return BadRequest("Quantity 0 er beshi hote hobe.");
        if (sale.Items.Any(i => i.UnitPrice < 0))
            return BadRequest("Unit price negative hote parbe na.");

        var ids = sale.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        var variants = await _db.ProductVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        if (ids.Except(variants.Keys).Any())
            return BadRequest("Kichu product pawa jay nai.");

        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();
        var productNames = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);

        foreach (var group in sale.Items.GroupBy(i => i.ProductVariantId))
        {
            var variant = variants[group.Key];
            var need = group.Sum(i => i.Quantity);
            if (variant.Quantity < need)
                return BadRequest($"'{productNames[variant.ProductId]} ({variant.Label})' er stock ache {variant.Quantity}, kintu chawa hoyeche {need}.");
        }

        foreach (var item in sale.Items)
        {
            var variant = variants[item.ProductVariantId];
            item.Id = 0;
            item.ProductId = variant.ProductId;
            item.ProductName = productNames[variant.ProductId];
            item.VariantName = variant.Label;
            item.CostPrice = variant.PurchasePrice;
            item.Total = item.Quantity * item.UnitPrice;

            variant.Quantity -= item.Quantity;
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
        sale.SoldBy = User.DisplayName();
        sale.Payments = new();
        sale.InvoiceNo = "TEMP";

        // Customer: Id dile oi customer, na hole phone diye khuje / notun banay
        var name = sale.CustomerName?.Trim();
        var phone = sale.CustomerPhone?.Trim();
        Customer? customer = null;
        if (sale.CustomerId.HasValue)
        {
            customer = await _db.Customers.FindAsync(sale.CustomerId.Value);
            if (customer == null) return BadRequest("Customer pawa jay nai.");
        }
        else if (!string.IsNullOrEmpty(phone))
        {
            phone = PhoneHelper.Normalize(phone);
            if (phone == null) return BadRequest("Customer er phone number thik nai. 01XXXXXXXXX (11 digit) hote hobe.");

            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == phone)
                    ?? new Customer { Name = string.IsNullOrEmpty(name) ? "Customer" : name, Phone = phone };
        }

        if (sale.DueAmount > 0 && customer == null)
            return BadRequest("Baki rakhte customer er phone number dite hobe.");

        // Invoice no er jonno Id lage, tai duibar save; transaction e rakha holo
        await using var tx = await _db.Database.BeginTransactionAsync();
        if (customer != null && customer.Id == 0)
        {
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }

        sale.CustomerId = customer?.Id;
        sale.CustomerName = customer?.Name ?? (string.IsNullOrEmpty(name) ? null : name);
        sale.CustomerPhone = customer?.Phone;

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
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return NotFound();

        var ids = sale.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        var variants = await _db.ProductVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id);

        foreach (var item in sale.Items)
            variants[item.ProductVariantId].Quantity += item.Quantity;

        // Items ar payments cascade e delete hoye jay
        _db.Sales.Remove(sale);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Kena dam shudhu admin dekhbe
    private void HideCost(IEnumerable<Sale> sales)
    {
        if (User.IsInRole(Roles.Admin)) return;
        foreach (var item in sales.SelectMany(s => s.Items))
            item.CostPrice = 0;
    }
}
