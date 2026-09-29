using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Api.Services;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

// Website er order: dekha, confirm (sale toiri), deliver (taka paoa), cancel
[ApiController]
[Route("api/weborders")]
[Permission(Perms.WebOrdersView)]
public class WebOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SaleWriter _sales;

    public WebOrdersController(AppDbContext db, SaleWriter sales)
    {
        _db = db;
        _sales = sales;
    }

    // GET: api/weborders?status=Pending
    [HttpGet]
    public async Task<ActionResult<List<WebOrder>>> GetAll(string? status)
    {
        var query = _db.WebOrders.AsNoTracking().Include(o => o.Items).AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);
        return await query.OrderByDescending(o => o.Id).Take(500).ToListAsync();
    }

    // GET: api/weborders/counts  -> tab er sonkha
    [HttpGet("counts")]
    public async Task<Dictionary<string, int>> Counts()
    {
        var counts = await _db.WebOrders.GroupBy(o => o.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        foreach (var s in WebOrderStatus.All) counts.TryAdd(s, 0);
        return counts;
    }

    // GET: api/weborders/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<WebOrder>> GetById(int id)
    {
        var order = await _db.WebOrders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        return order;
    }

    // GET: api/weborders/variants  -> order edit e product khuje add korar jonno
    [HttpGet("variants")]
    [Permission(Perms.WebOrdersEdit)]
    public async Task<List<OrderVariantOption>> Variants()
    {
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active && p.Variants.Any())
            .Include(p => p.Variants)
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .AsSplitQuery()
            .OrderBy(p => p.Name)
            .ToListAsync();

        return products.SelectMany(p => p.Variants.OrderBy(v => v.Id).Select(v => new OrderVariantOption
        {
            VariantId = v.Id, ProductId = p.Id, ProductName = p.Name, Code = p.Code, Label = v.Label, Sku = v.Sku,
            Price = p.PriceAfterDiscount(v.Price), Stock = v.Quantity, Image = p.Images.Select(i => i.Url).FirstOrDefault()
        })).ToList();
    }

    // PUT: api/weborders/5
    // Confirm er age: qty / dam bodlano, notun product add / bad, discount, delivery charge, advance
    [HttpPut("{id:int}")]
    [Permission(Perms.WebOrdersEdit)]
    public async Task<ActionResult<WebOrder>> Update(int id, WebOrderUpdateRequest request)
    {
        var order = await _db.WebOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        if (order.Status != WebOrderStatus.Pending) return BadRequest("Shudhu Pending order bodlano jay.");

        var phone = PhoneHelper.Normalize(request.Phone);
        if (phone == null) return BadRequest("Phone number thik nai. 01XXXXXXXXX (11 digit) hote hobe.");
        if (request.Items.Count == 0) return BadRequest("Kompokkhe ekta product rakhun.");
        if (request.Items.Any(i => i.Quantity <= 0 || i.Quantity > 1000)) return BadRequest("Quantity 1 - 1000 er moddhe hote hobe.");
        if (request.Items.Any(i => i.UnitPrice < 0)) return BadRequest("Dam negative hote parbe na.");

        // Same size/color duibar dile ek line e jog
        var lines = request.Items.GroupBy(i => i.VariantId)
            .Select(g => new WebOrderLine { VariantId = g.Key, Quantity = g.Sum(x => x.Quantity), UnitPrice = g.First().UnitPrice })
            .ToList();
        var ids = lines.Select(l => l.VariantId).ToList();
        var variants = await _db.ProductVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        if (variants.Count != ids.Count) return BadRequest("Kichu product pawa jay nai.");
        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();
        var names = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);

        var items = lines.Select(l =>
        {
            var v = variants[l.VariantId];
            return new WebOrderItem
            {
                ProductId = v.ProductId, ProductVariantId = v.Id, ProductName = names[v.ProductId], VariantName = v.Label,
                Quantity = l.Quantity, UnitPrice = l.UnitPrice, Total = l.Quantity * l.UnitPrice
            };
        }).ToList();

        var subTotal = items.Sum(i => i.Total);
        if (request.Discount < 0 || request.Discount > subTotal) return BadRequest("Discount 0 theke sub total er moddhe hote hobe.");
        if (request.DeliveryCharge < 0) return BadRequest("Delivery charge negative hote parbe na.");
        var total = subTotal - request.Discount + request.DeliveryCharge;
        if (request.Advance < 0 || request.Advance > total) return BadRequest("Advance 0 theke grand total er moddhe hote hobe.");

        _db.WebOrderItems.RemoveRange(order.Items);
        order.Items = items;
        order.CustomerName = request.CustomerName.Trim();
        order.Phone = phone;
        order.Address = request.Address.Trim();
        order.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        order.SubTotal = subTotal;
        order.Discount = request.Discount;
        order.DeliveryCharge = request.DeliveryCharge;
        order.Total = total;
        order.Advance = request.Advance;
        order.HandledBy = User.DisplayName();
        order.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return order;
    }

    // POST: api/weborders/5/confirm
    // Sale (invoice) toiri hoy, stock kome, customer save hoy. Advance paid, baki COD - deliver e joma.
    [HttpPost("{id:int}/confirm")]
    [Permission(Perms.WebOrdersEdit)]
    public async Task<ActionResult<WebOrder>> Confirm(int id)
    {
        var order = await _db.WebOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        if (order.Status != WebOrderStatus.Pending) return BadRequest($"Ei order already {order.Status}.");

        var sale = new Sale
        {
            SaleDate = DateTime.Today,
            CustomerName = order.CustomerName,
            CustomerPhone = order.Phone,
            Note = $"Web order {order.OrderNo} · {order.Address}" + (order.Note != null ? $" · {order.Note}" : "")
                 + (order.Advance > 0 ? $" · Advance {order.Advance:N0}" : ""),
            Discount = order.Discount,
            DeliveryCharge = order.DeliveryCharge,
            PaidAmount = order.Advance,
            Items = order.Items.Select(i => new SaleItem { ProductVariantId = i.ProductVariantId, Quantity = i.Quantity, UnitPrice = i.UnitPrice }).ToList()
        };

        await using var tx = await _db.Database.BeginTransactionAsync();
        var (created, error) = await _sales.CreateAsync(sale, User.DisplayName());
        if (error != null)
            return BadRequest(error + " Age Stock In korun, tarpor confirm korun.");

        // Customer er thikana na thakle order er thikana boshano
        var customer = await _db.Customers.FindAsync(created!.CustomerId);
        if (customer != null && string.IsNullOrWhiteSpace(customer.Address))
            customer.Address = order.Address.Length > 200 ? order.Address[..200] : order.Address;

        order.Status = WebOrderStatus.Confirmed;
        order.SaleId = created.Id;
        order.InvoiceNo = created.InvoiceNo;
        order.HandledBy = User.DisplayName();
        order.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return order;
    }

    // POST: api/weborders/5/deliver
    // Mal pouche geche, delivery man taka (COD) niye asheche
    [HttpPost("{id:int}/deliver")]
    [Permission(Perms.WebOrdersEdit)]
    public async Task<ActionResult<WebOrder>> Deliver(int id)
    {
        var order = await _db.WebOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        if (order.Status != WebOrderStatus.Confirmed) return BadRequest("Age order confirm korun.");

        var sale = order.SaleId == null ? null : await _db.Sales.FindAsync(order.SaleId);
        if (sale != null && sale.DueAmount > 0)
        {
            _db.SalePayments.Add(new SalePayment
            {
                SaleId = sale.Id,
                InvoiceNo = sale.InvoiceNo,
                PaymentDate = DateTime.Today,
                Amount = sale.DueAmount,
                Note = $"COD · {order.OrderNo}",
                ReceivedBy = User.DisplayName()
            });
            sale.PaidAmount += sale.DueAmount;
            sale.DueAmount = 0;
        }

        order.Status = WebOrderStatus.Delivered;
        order.DeliveredAt = DateTime.Now;
        order.HandledBy = User.DisplayName();
        order.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return order;
    }

    // POST: api/weborders/5/cancel
    // Confirm kora thakle sale bad hoy ar stock ferot ashe
    [HttpPost("{id:int}/cancel")]
    [Permission(Perms.WebOrdersEdit)]
    public async Task<ActionResult<WebOrder>> Cancel(int id, CancelWebOrderRequest request)
    {
        var order = await _db.WebOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        if (order.Status is WebOrderStatus.Delivered or WebOrderStatus.Cancelled)
            return BadRequest($"{order.Status} order cancel kora jay na.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        if (order.SaleId != null)
        {
            var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == order.SaleId);
            if (sale != null) await _sales.DeleteAsync(sale);
            order.SaleId = null;
        }

        order.Status = WebOrderStatus.Cancelled;
        order.CancelReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        order.HandledBy = User.DisplayName();
        order.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return order;
    }
}
