using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Api.Services;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private const string ImageFolder = "uploads/products/";
    private const long MaxImageBytes = 2 * 1024 * 1024;
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png" };

    private readonly AppDbContext _db;
    private readonly string _imageDir;

    public ProductsController(AppDbContext db, IWebHostEnvironment env, IConfiguration config)
    {
        _db = db;
        _imageDir = Path.Combine(UploadPaths.Root(config, env), "products");
    }

    private IQueryable<Product> WithDetails => _db.Products
        .AsNoTracking()
        .Include(p => p.Category)
        .Include(p => p.Brand)
        .Include(p => p.Shop)
        .Include(p => p.Supplier)
        .Include(p => p.Variants.OrderBy(v => v.Id))
        .Include(p => p.Images.OrderBy(i => i.SortOrder))
        .AsSplitQuery();

    // GET: api/products
    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll()
    {
        var products = await WithDetails.OrderBy(p => p.Name).ToListAsync();
        await HideCostAsync(products);
        return products;
    }

    // GET: api/products/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetById(int id)
    {
        var product = await WithDetails.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();
        await HideCostAsync(new[] { product });
        return product;
    }

    // Kena dam shudhu admin dekhbe
    private async Task HideCostAsync(IEnumerable<Product> products)
    {
        if (await HttpContext.RequestServices.GetRequiredService<PermissionService>().HasAsync(User, Perms.CostView)) return;
        foreach (var p in products)
        {
            p.PurchasePrice = 0;
            foreach (var v in p.Variants)
                v.PurchasePrice = 0;
        }
    }

    // POST: api/products/images
    // Chobi save kore relative url ferot dey, product save er shomoy url ta pathate hoy
    [HttpPost("images")]
    [Permission(Perms.ProductsAdd, Perms.ProductsEdit)]
    [RequestSizeLimit(MaxImageBytes + 64 * 1024)]
    public async Task<ActionResult<ProductImage>> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("Kono file nai.");
        if (file.Length > MaxImageBytes) return BadRequest("Chobi 2MB er beshi hote parbe na.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ImageExtensions.Contains(ext)) return BadRequest("Shudhu JPG / PNG chobi deya jabe.");

        Directory.CreateDirectory(_imageDir);
        var name = $"{Guid.NewGuid():N}{ext}";

        await using (var stream = System.IO.File.Create(Path.Combine(_imageDir, name)))
            await file.CopyToAsync(stream);

        return new ProductImage { Url = ImageFolder + name };
    }

    private void DeleteImageFiles(IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            var path = Path.Combine(_imageDir, Path.GetFileName(url));
            try { System.IO.File.Delete(path); } catch (IOException) { }
        }
    }

    // POST: api/products
    // Variant er Quantity ekhane opening stock hisebe dhora hoy
    [HttpPost]
    [Permission(Perms.ProductsAdd)]
    public async Task<ActionResult<Product>> Create(Product product)
    {
        Normalize(product);
        var error = await ValidateAsync(product, 0);
        if (error != null) return BadRequest(error);

        product.Id = 0;
        product.Category = null;
        product.Brand = null;
        product.Shop = null;
        product.Supplier = null;
        product.CreatedAt = DateTime.Now;
        foreach (var v in product.Variants)
        {
            v.Id = 0;
            v.ProductId = 0;
        }
        foreach (var i in product.Images)
        {
            i.Id = 0;
            i.ProductId = 0;
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
    [Permission(Perms.ProductsEdit)]
    public async Task<IActionResult> Update(int id, Product product)
    {
        if (id != product.Id) return BadRequest("Id mismatch");

        var existing = await _db.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id);
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
                      ?? await _db.StockEntryItems.Where(i => removedIds.Contains(i.ProductVariantId)).Select(i => (int?)i.ProductVariantId).FirstOrDefaultAsync()
                      ?? await _db.WebOrderItems.Where(i => removedIds.Contains(i.ProductVariantId)).Select(i => (int?)i.ProductVariantId).FirstOrDefaultAsync();
            if (usedId != null)
                return BadRequest($"'{removed.First(v => v.Id == usedId).Label}' er sale ba stock entry ache, tai remove kora jabe na.");
        }

        existing.Name = product.Name;
        existing.Code = product.Code;
        existing.Barcode = product.Barcode;
        existing.Description = product.Description;
        existing.CategoryId = product.CategoryId;
        existing.BrandId = product.BrandId;
        existing.Gender = product.Gender;
        existing.Fabric = product.Fabric;
        existing.FitType = product.FitType;
        existing.Sleeve = product.Sleeve;
        existing.Season = product.Season;
        existing.PurchasePrice = product.PurchasePrice;
        existing.Price = product.Price;
        existing.Discount = product.Discount;
        existing.DiscountIsPercent = product.DiscountIsPercent;
        existing.VatPercent = product.VatPercent;
        existing.ShopId = product.ShopId;
        existing.SupplierId = product.SupplierId;
        existing.Status = product.Status;
        existing.Tags = product.Tags;

        // Chobi gulo notun list diye replace, bad pora file gulo muche fela hoy
        var keepUrls = product.Images.Select(i => i.Url).ToHashSet();
        var removedUrls = existing.Images.Select(i => i.Url).Where(u => !keepUrls.Contains(u)).ToList();
        _db.ProductImages.RemoveRange(existing.Images);
        existing.Images = product.Images.Select(i => new ProductImage { Url = i.Url, SortOrder = i.SortOrder }).ToList();

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

        DeleteImageFiles(removedUrls);
        return NoContent();
    }

    // DELETE: api/products/5
    [HttpDelete("{id:int}")]
    [Permission(Perms.ProductsDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        var used = await _db.SaleItems.AnyAsync(i => i.ProductId == id)
                || await _db.StockEntryItems.AnyAsync(i => i.ProductId == id)
                || await _db.WebOrderItems.AnyAsync(i => i.ProductId == id);
        if (used)
            return BadRequest($"'{product.Name}' er stock entry ba sale ache, tai delete kora jabe na.");

        // Variant ar chobi gulo cascade e delete hoye jay
        var urls = product.Images.Select(i => i.Url).ToList();
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        DeleteImageFiles(urls);
        return NoContent();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Normalize(Product product)
    {
        product.Name = product.Name.Trim();
        product.Code = Clean(product.Code);
        product.Barcode = Clean(product.Barcode);
        product.Description = Clean(product.Description);
        product.Gender = Clean(product.Gender);
        product.Fabric = Clean(product.Fabric);
        product.FitType = Clean(product.FitType);
        product.Sleeve = Clean(product.Sleeve);
        product.Season = Clean(product.Season);
        product.Tags = Clean(product.Tags);
        product.Status = Clean(product.Status) ?? ProductStatus.Active;

        product.Images = product.Images
            .Where(i => !string.IsNullOrWhiteSpace(i.Url))
            .Select((i, index) => new ProductImage { Id = i.Id, Url = i.Url.Trim(), SortOrder = index })
            .ToList();

        foreach (var v in product.Variants)
        {
            v.Size = (v.Size ?? string.Empty).Trim();
            v.Color = (v.Color ?? string.Empty).Trim();
            v.Sku = string.IsNullOrWhiteSpace(v.Sku) ? null : v.Sku.Trim();
        }
    }

    private async Task<string?> ValidateAsync(Product product, int id)
    {
        if (!ProductStatus.All.Contains(product.Status))
            return "Status thik nai.";
        // Draft e variant chara save kora jay
        if (product.Variants.Count == 0 && product.Status != ProductStatus.Draft)
            return "Kompokkhe ekta size/color add korun.";
        if (product.Discount < 0 || product.Price < 0 || product.PurchasePrice < 0)
            return "Dam ba discount negative hote parbe na.";
        if (product.DiscountIsPercent && product.Discount > 100)
            return "Discount 100% er beshi hote parbe na.";
        if (product.Images.Any(i => !i.Url.StartsWith(ImageFolder) || i.Url.Contains("..")))
            return "Chobi thik nai.";
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
        if (product.ShopId.HasValue && !await _db.Shops.AnyAsync(s => s.Id == product.ShopId))
            return "Shop pawa jay nai.";
        if (product.SupplierId.HasValue && !await _db.Suppliers.AnyAsync(s => s.Id == product.SupplierId))
            return "Supplier pawa jay nai.";

        return null;
    }

    // SKU khali thakle product Id + variant Id diye banano hoy, jemon 00070012
    private static void FillMissingSkus(IEnumerable<ProductVariant> variants)
    {
        foreach (var v in variants.Where(v => v.Sku == null))
            v.Sku = $"{v.ProductId:D4}{v.Id:D4}";
    }
}
