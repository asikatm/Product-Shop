using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

// Customer der website (login chara). Shudhu Active product; kena dam / stock er sonkha jay na.
[ApiController]
[Route("api/store")]
[AllowAnonymous]
public class StoreController : ControllerBase
{
    private const int MaxItems = 20;
    private const int MaxQty = 10;

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public StoreController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    private decimal DeliveryCharge => _config.GetValue("Store:DeliveryCharge", 0m);

    private IQueryable<Product> ActiveProducts => _db.Products
        .AsNoTracking()
        .Where(p => p.Status == ProductStatus.Active && p.Variants.Any())
        .Include(p => p.Category)
        .Include(p => p.Brand)
        .Include(p => p.Variants.OrderBy(v => v.Id))
        .Include(p => p.Images.OrderBy(i => i.SortOrder))
        .AsSplitQuery();

    // GET: api/store/info
    [HttpGet("info")]
    public StoreInfo Info() => new() { DeliveryCharge = DeliveryCharge };

    // GET: api/store/categories
    [HttpGet("categories")]
    public async Task<List<StoreCategory>> Categories()
    {
        return await _db.Products
            .Where(p => p.Status == ProductStatus.Active && p.CategoryId != null && p.Variants.Any())
            .GroupBy(p => new { p.CategoryId, p.Category!.Name })
            .Select(g => new StoreCategory { Id = g.Key.CategoryId!.Value, Name = g.Key.Name, ProductCount = g.Count() })
            .OrderByDescending(c => c.ProductCount)
            .ToListAsync();
    }

    // GET: api/store/products?categoryId=3&q=jamdani
    [HttpGet("products")]
    public async Task<List<StoreProduct>> Products(int? categoryId, string? q)
    {
        var query = ActiveProducts;
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.Name.Contains(term) || (p.Code != null && p.Code.Contains(term)));
        }

        var products = await query.OrderByDescending(p => p.Id).Take(200).ToListAsync();
        return products.Select(ToStore).ToList();
    }

    // GET: api/store/products/5
    [HttpGet("products/{id:int}")]
    public async Task<ActionResult<StoreProduct>> Product(int id)
    {
        var product = await ActiveProducts.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();
        return ToStore(product);
    }

    // POST: api/store/orders
    // Dam server e hisab hoy (browser theke asha dam bishshash kora hoy na)
    [HttpPost("orders")]
    [EnableRateLimiting("store-orders")]
    public async Task<ActionResult<PlaceOrderResponse>> PlaceOrder(PlaceOrderRequest request)
    {
        var phone = PhoneHelper.Normalize(request.Phone);
        if (phone == null) return BadRequest("Mobile number thik nai. 11 digit er 01XXXXXXXXX likhun.");

        var lines = request.Items.Where(i => i.Quantity > 0).GroupBy(i => i.VariantId)
            .Select(g => new PlaceOrderItem { VariantId = g.Key, Quantity = g.Sum(i => i.Quantity) }).ToList();
        if (lines.Count == 0) return BadRequest("Cart e kono product nai.");
        if (lines.Count > MaxItems) return BadRequest($"Ek order e {MaxItems} ta porjonto product deya jay.");
        if (lines.Any(i => i.Quantity > MaxQty)) return BadRequest($"Protiti product {MaxQty} ta porjonto order kora jay. Beshi lagle phone korun.");

        var ids = lines.Select(i => i.VariantId).ToList();
        var variants = await _db.ProductVariants.Where(v => ids.Contains(v.Id)).ToListAsync();
        var productIds = variants.Select(v => v.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id) && p.Status == ProductStatus.Active).ToDictionaryAsync(p => p.Id);
        if (variants.Count != ids.Count || variants.Any(v => !products.ContainsKey(v.ProductId)))
            return BadRequest("Kichu product ekhon ar pawa jacche na. Page reload kore abar chesta korun.");

        var order = new WebOrder
        {
            OrderNo = "TEMP",
            CustomerName = request.Name.Trim(),
            Phone = phone,
            Address = request.Address.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            DeliveryCharge = DeliveryCharge
        };

        foreach (var line in lines)
        {
            var v = variants.First(x => x.Id == line.VariantId);
            var p = products[v.ProductId];
            var price = p.PriceAfterDiscount(v.Price);
            order.Items.Add(new WebOrderItem
            {
                ProductId = p.Id,
                ProductVariantId = v.Id,
                ProductName = p.Name.Length > 100 ? p.Name[..100] : p.Name,
                VariantName = v.Label,
                Quantity = line.Quantity,
                UnitPrice = price,
                Total = price * line.Quantity
            });
        }

        order.SubTotal = order.Items.Sum(i => i.Total);
        order.Total = order.SubTotal + order.DeliveryCharge;

        // Order no er jonno Id lage
        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.WebOrders.Add(order);
        await _db.SaveChangesAsync();
        order.OrderNo = $"WEB-{order.Id:D6}";
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return new PlaceOrderResponse { OrderNo = order.OrderNo, Total = order.Total };
    }

    // GET: api/store/orders/WEB-000001?phone=017...
    // Customer nijer order dekhte pare (order no + phone mille)
    [HttpGet("orders/{orderNo}")]
    public async Task<ActionResult<OrderTrack>> Track(string orderNo, string? phone)
    {
        var normalized = PhoneHelper.Normalize(phone);
        var order = await _db.WebOrders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNo == orderNo && o.Phone == normalized);
        if (order == null) return NotFound();

        return new OrderTrack
        {
            OrderNo = order.OrderNo,
            Status = order.Status,
            CustomerName = order.CustomerName,
            Total = order.Total,
            CreatedAt = order.CreatedAt,
            Items = order.Items
        };
    }

    private static StoreProduct ToStore(Product p)
    {
        var cheapest = p.Variants.OrderBy(v => p.PriceAfterDiscount(v.Price)).First();
        return new StoreProduct
        {
            Id = p.Id,
            Name = p.Name,
            Code = p.Code,
            Description = p.Description,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name,
            BrandName = p.Brand?.Name,
            Fabric = p.Fabric,
            Price = cheapest.Price,
            FinalPrice = p.PriceAfterDiscount(cheapest.Price),
            Images = p.Images.Select(i => i.Url).ToList(),
            Variants = p.Variants.Select(v => new StoreVariant
            {
                Id = v.Id,
                Size = v.Size,
                Color = v.Color,
                Price = v.Price,
                FinalPrice = p.PriceAfterDiscount(v.Price),
                InStock = v.Quantity > 0
            }).ToList()
        };
    }
}
