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

    // POST: api/weborders/5/confirm
    // Sale (invoice) toiri hoy, stock kome, customer save hoy. Taka COD - pore deliver e joma.
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
            Note = $"Web order {order.OrderNo} · {order.Address}" + (order.Note != null ? $" · {order.Note}" : ""),
            PaidAmount = 0,
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
