using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db)
    {
        _db = db;
    }

    private IQueryable<Product> WithDetails => _db.Products
        .AsNoTracking()
        .Include(p => p.Category)
        .Include(p => p.Brand)
        .Include(p => p.Variants.OrderBy(v => v.Id))
        .AsSplitQuery();

    // GET: api/products
    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll()
    {
        var products = await WithDetails.OrderBy(p => p.Name).ToListAsync();
        HideCost(products);
        return products;
    }

    // GET: api/products/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetById(int id)
    {
        var product = await WithDetails.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();
        HideCost(new[] { product });
        return product;
    }

    // Kena dam shudhu admin dekhbe
    private void HideCost(IEnumerable<Product> products)
    {
        if (User.IsInRole(Roles.Admin)) return;
        foreach (var v in products.SelectMany(p => p.Variants))
            v.PurchasePrice = 0;
    }

    // POST: api/products
    // Variant er Quantity ekhane opening stock hisebe dhora hoy
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<Product>> Create(Product product)
    {
        Normalize(product);
        var error = await ValidateAsync(product, 0);
        if (error != null) return BadRequest(error);

        product.Id = 0;
        product.Category = null;
        product.Brand = null;
        product.CreatedAt = DateTime.Now;
        foreach (var v in product.Variants)
        {
            v.Id = 0;
            v.ProductId = 0;
        }

        // SKU banate variant er Id lage, tai duibar save
        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        FillMissingSkus(product.Variants);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    // PUT: api/products/5
    // Purono variant er stock ekhane change hoy na, Stock In / Sale diye hoy.
    // Notun variant er Quantity opening stock.
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, Product product)
    {
        if (id != product.Id) return BadRequest("Id mismatch");

        var existing = await _db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id);
        if (existing == null) return NotFound();

        Normalize(product);
        var error = await ValidateAsync(product, id);
        if (error != null) return BadRequest(error);

        var keepIds = product.Variants.Where(v => v.Id > 0).Select(v => v.Id).ToHashSet();
        if (keepIds.Except(existing.Variants.Select(v => v.Id)).Any())
            return BadRequest("Kichu size/color ei product er na.");

        var removed = existing.Variants.Where(v => !keepIds.Contains(v.Id)).ToList();
        if (removed.Count > 0)
        {
            var removedIds = removed.Select(v => v.Id).ToList();
            var usedId = await _db.SaleItems.Where(i => removedIds.Contains(i.ProductVariantId)).Select(i => (int?)i.ProductVariantId).FirstOrDefaultAsync()
                      ?? await _db.StockEntryItems.Where(i => removedIds.Contains(i.ProductVariantId)).Select(i => (int?)i.ProductVariantId).FirstOrDefaultAsync();
            if (usedId != null)
                return BadRequest($"'{removed.First(v => v.Id == usedId).Label}' er sale ba stock entry ache, tai remove kora jabe na.");
        }

        existing.Name = product.Name;
        existing.Code = product.Code;
        existing.Description = product.Description;
        existing.CategoryId = product.CategoryId;
        existing.BrandId = product.BrandId;

        _db.ProductVariants.RemoveRange(removed);
        foreach (var v in product.Variants)
        {
            var target = v.Id > 0 ? existing.Variants.First(x => x.Id == v.Id) : null;
            if (target == null)
            {
                target = new ProductVariant { Quantity = v.Quantity };
                existing.Variants.Add(target);
            }

            target.Size = v.Size;
            target.Color = v.Color;
            target.Sku = v.Sku;
            target.PurchasePrice = v.PurchasePrice;
            target.Price = v.Price;
            target.ReorderLevel = v.ReorderLevel;
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.SaveChangesAsync();
        FillMissingSkus(existing.Variants);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return NoContent();
    }

    // DELETE: api/products/5
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        var used = await _db.SaleItems.AnyAsync(i => i.ProductId == id)
                || await _db.StockEntryItems.AnyAsync(i => i.ProductId == id);
        if (used)
            return BadRequest($"'{product.Name}' er stock entry ba sale ache, tai delete kora jabe na.");

        // Variant gulo cascade e delete hoye jay
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void Normalize(Product product)
    {
        product.Name = product.Name.Trim();
        product.Code = string.IsNullOrWhiteSpace(product.Code) ? null : product.Code.Trim();
        product.Description = string.IsNullOrWhiteSpace(product.Description) ? null : product.Description.Trim();

        foreach (var v in product.Variants)
        {
            v.Size = (v.Size ?? string.Empty).Trim();
            v.Color = (v.Color ?? string.Empty).Trim();
            v.Sku = string.IsNullOrWhiteSpace(v.Sku) ? null : v.Sku.Trim();
        }
    }

    private async Task<string?> ValidateAsync(Product product, int id)
    {
        if (product.Variants.Count == 0)
            return "Kompokkhe ekta size/color add korun.";
        if (product.Variants.Any(v => v.Size.Length == 0))
            return "Protiti row te size dite hobe.";

        var duplicate = product.Variants
            .GroupBy(v => (v.Size.ToUpperInvariant(), v.Color.ToUpperInvariant()))
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
            return $"'{duplicate.First().Label}' duibar deya hoyeche.";

        var skus = product.Variants.Where(v => v.Sku != null).Select(v => v.Sku!).ToList();
        var duplicateSku = skus.GroupBy(s => s.ToUpperInvariant()).FirstOrDefault(g => g.Count() > 1);
        if (duplicateSku != null)
            return $"SKU '{duplicateSku.First()}' duibar deya hoyeche.";

        var takenSku = await _db.ProductVariants
            .Where(v => v.ProductId != id && v.Sku != null && skus.Contains(v.Sku))
            .Select(v => v.Sku)
            .FirstOrDefaultAsync();
        if (takenSku != null)
            return $"SKU '{takenSku}' onno product e ache.";

        if (product.CategoryId.HasValue && !await _db.Categories.AnyAsync(c => c.Id == product.CategoryId))
            return "Category pawa jay nai.";
        if (product.BrandId.HasValue && !await _db.Brands.AnyAsync(b => b.Id == product.BrandId))
            return "Brand pawa jay nai.";

        return null;
    }

    // SKU khali thakle product Id + variant Id diye banano hoy, jemon 00070012
    private static void FillMissingSkus(IEnumerable<ProductVariant> variants)
    {
        foreach (var v in variants.Where(v => v.Sku == null))
            v.Sku = $"{v.ProductId:D4}{v.Id:D4}";
    }
}
