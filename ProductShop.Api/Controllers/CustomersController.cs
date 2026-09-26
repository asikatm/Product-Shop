using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Api.Services;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/customers
    // Protiti customer er mot kena ar baki shoho
    [HttpGet]
    public async Task<ActionResult<List<Customer>>> GetAll()
    {
        var customers = await _db.Customers.OrderBy(c => c.Name).ToListAsync();
        var totals = await _db.Sales
            .Where(s => s.CustomerId != null)
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new { Id = g.Key, Total = g.Sum(s => s.GrandTotal), Due = g.Sum(s => s.DueAmount) })
            .ToDictionaryAsync(x => x.Id);

        foreach (var c in customers)
        {
            if (!totals.TryGetValue(c.Id, out var t)) continue;
            c.TotalPurchase = t.Total;
            c.TotalDue = t.Due;
        }
        return customers;
    }

    // GET: api/customers/5
    // Customer, tar shob sale ar baki joma er hisab
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDetails>> GetById(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        var sales = await _db.Sales
            .Include(s => s.Items)
            .Where(s => s.CustomerId == id)
            .OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.Id)
            .ToListAsync();
        var saleIds = sales.Select(s => s.Id).ToList();
        var payments = await _db.SalePayments
            .Where(p => saleIds.Contains(p.SaleId))
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id)
            .ToListAsync();

        customer.TotalPurchase = sales.Sum(s => s.GrandTotal);
        customer.TotalDue = sales.Sum(s => s.DueAmount);
        return new CustomerDetails { Customer = customer, Sales = sales, Payments = payments };
    }

    // POST: api/customers
    [HttpPost]
    public async Task<ActionResult<Customer>> Create(Customer customer)
    {
        Normalize(customer);
        if (await _db.Customers.AnyAsync(c => c.Phone == customer.Phone))
            return BadRequest($"{customer.Phone} number e already customer ache.");

        customer.Id = 0;
        customer.CreatedAt = DateTime.Now;
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    // PUT: api/customers/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Customer customer)
    {
        if (id != customer.Id) return BadRequest("Id mismatch");

        var existing = await _db.Customers.FindAsync(id);
        if (existing == null) return NotFound();

        Normalize(customer);
        if (await _db.Customers.AnyAsync(c => c.Phone == customer.Phone && c.Id != id))
            return BadRequest($"{customer.Phone} number e onno customer ache.");

        existing.Name = customer.Name;
        existing.Phone = customer.Phone;
        existing.Address = customer.Address;

        // Purono invoice e o notun naam / phone dekhabe
        var sales = await _db.Sales.Where(s => s.CustomerId == id).ToListAsync();
        foreach (var s in sales)
        {
            s.CustomerName = existing.Name;
            s.CustomerPhone = existing.Phone;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/customers/5
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        if (await _db.Sales.AnyAsync(s => s.CustomerId == id))
            return BadRequest($"'{customer.Name}' er sale ache, tai delete kora jabe na.");

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST: api/customers/5/payments
    // Baki joma: shobcheye purono baki invoice theke age shodh hoy
    [HttpPost("{id:int}/payments")]
    public async Task<ActionResult<List<SalePayment>>> ReceivePayment(int id, ReceivePaymentRequest request)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == id)) return NotFound();

        var dueSales = await _db.Sales
            .Where(s => s.CustomerId == id && s.DueAmount > 0)
            .OrderBy(s => s.SaleDate).ThenBy(s => s.Id)
            .ToListAsync();
        var totalDue = dueSales.Sum(s => s.DueAmount);

        if (totalDue == 0)
            return BadRequest("Ei customer er kono baki nai.");
        if (request.Amount > totalDue)
            return BadRequest($"Mot baki {totalDue:N2}, er beshi joma newa jabe na.");

        var remaining = request.Amount;
        var payments = new List<SalePayment>();
        foreach (var sale in dueSales)
        {
            if (remaining <= 0) break;

            var pay = Math.Min(remaining, sale.DueAmount);
            sale.PaidAmount += pay;
            sale.DueAmount -= pay;
            remaining -= pay;

            payments.Add(new SalePayment
            {
                SaleId = sale.Id,
                InvoiceNo = sale.InvoiceNo,
                PaymentDate = request.PaymentDate.Date,
                Amount = pay,
                Note = request.Note,
                ReceivedBy = User.DisplayName()
            });
        }

        _db.SalePayments.AddRange(payments);
        await _db.SaveChangesAsync();
        return payments;
    }

    // DELETE: api/customers/payments/5
    // Vul joma bad dile invoice e abar baki fire ashe
    [HttpDelete("payments/{paymentId:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeletePayment(int paymentId)
    {
        var payment = await _db.SalePayments.FindAsync(paymentId);
        if (payment == null) return NotFound();

        var sale = await _db.Sales.FindAsync(payment.SaleId);
        if (sale != null)
        {
            sale.PaidAmount -= payment.Amount;
            sale.DueAmount += payment.Amount;
        }

        _db.SalePayments.Remove(payment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void Normalize(Customer customer)
    {
        customer.Name = customer.Name.Trim();
        customer.Phone = customer.Phone.Trim();
        customer.Address = string.IsNullOrWhiteSpace(customer.Address) ? null : customer.Address.Trim();
    }
}
