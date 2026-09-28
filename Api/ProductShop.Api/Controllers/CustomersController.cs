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
    [Permission(Perms.CustomersView, Perms.SalesAdd)]
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
    [Permission(Perms.CustomersView)]
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
    [Permission(Perms.CustomersAdd)]
    public async Task<ActionResult<Customer>> Create(Customer customer)
    {
        var error = Normalize(customer);
        if (error != null) return BadRequest(error);
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
    [Permission(Perms.CustomersEdit)]
    public async Task<IActionResult> Update(int id, Customer customer)
    {
        if (id != customer.Id) return BadRequest("Id mismatch");

        var existing = await _db.Customers.FindAsync(id);
        if (existing == null) return NotFound();

        var error = Normalize(customer);
        if (error != null) return BadRequest(error);
        if (await _db.Customers.AnyAsync(c => c.Phone == customer.Phone && c.Id != id))
            return BadRequest($"{customer.Phone} number e onno customer ache.");

        existing.Name = customer.Name;
        existing.Phone = customer.Phone;
        existing.AltPhone = customer.AltPhone;
        existing.Email = customer.Email;
        existing.Gender = customer.Gender;
        existing.DateOfBirth = customer.DateOfBirth;
        existing.CustomerType = customer.CustomerType;
        existing.Address = customer.Address;
        existing.City = customer.City;
        existing.Note = customer.Note;

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
    [Permission(Perms.CustomersDelete)]
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
    [Permission(Perms.CustomersEdit)]
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
    [Permission(Perms.CustomersDelete)]
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

    // Trim kore, phone 01XXXXXXXXX format e ane; vul thakle error message dey
    private static string? Normalize(Customer customer)
    {
        customer.Name = customer.Name.Trim();
        if (customer.Name.Length == 0) return "Customer er naam dite hobe.";

        var phone = PhoneHelper.Normalize(customer.Phone);
        if (phone == null) return "Phone number thik nai. 01XXXXXXXXX (11 digit) hote hobe.";
        customer.Phone = phone;

        if (string.IsNullOrWhiteSpace(customer.AltPhone))
            customer.AltPhone = null;
        else
        {
            customer.AltPhone = PhoneHelper.Normalize(customer.AltPhone);
            if (customer.AltPhone == null) return "Alternate phone number thik nai.";
            if (customer.AltPhone == customer.Phone) customer.AltPhone = null;
        }

        if (!CustomerTypes.All.Contains(customer.CustomerType)) customer.CustomerType = CustomerTypes.Regular;
        if (customer.Gender != null && !CustomerOptions.Genders.Contains(customer.Gender)) customer.Gender = null;
        if (customer.DateOfBirth > DateTime.Today) return "Jonmo tarikh vobisshoter hote parbe na.";

        customer.Email = Clean(customer.Email)?.ToLowerInvariant();
        customer.Address = Clean(customer.Address);
        customer.City = Clean(customer.City);
        customer.Note = Clean(customer.Note);
        customer.DateOfBirth = customer.DateOfBirth?.Date;
        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
