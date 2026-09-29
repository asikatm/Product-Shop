using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Shared;

namespace ProductShop.Api.Services;

// Sale banano / bad deya - New Sale form ar Web Order confirm duitai ekhan theke kore
public class SaleWriter
{
    private readonly AppDbContext _db;

    public SaleWriter(AppDbContext db)
    {
        _db = db;
    }

    // Stock check kore sale save kore, stock komay. Vul hole Error e karon.
    public async Task<(Sale? Sale, string? Error)> CreateAsync(Sale sale, string soldBy)
    {
        if (sale.Items.Count == 0) return (null, "Kompokkhe ekta product add korun.");
        if (sale.Items.Any(i => i.Quantity <= 0)) return (null, "Quantity 0 er beshi hote hobe.");
        if (sale.Items.Any(i => i.UnitPrice < 0)) return (null, "Unit price negative hote parbe na.");

        var ids = sale.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        var variants = await _db.ProductVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        if (ids.Except(variants.Keys).Any()) return (null, "Kichu product pawa jay nai.");

        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();
        var productNames = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);

        foreach (var group in sale.Items.GroupBy(i => i.ProductVariantId))
        {
            var variant = variants[group.Key];
            var need = group.Sum(i => i.Quantity);
            if (variant.Quantity < need)
                return (null, $"'{productNames[variant.ProductId]} ({variant.Label})' er stock ache {variant.Quantity}, kintu chawa hoyeche {need}.");
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
        if (sale.Discount < 0 || sale.Discount > sale.SubTotal) return (null, "Discount 0 theke sub total er moddhe hote hobe.");
        if (sale.PaidAmount < 0) return (null, "Paid amount negative hote parbe na.");
        if (sale.DeliveryCharge < 0) return (null, "Delivery charge negative hote parbe na.");

        sale.Id = 0;
        sale.SaleDate = sale.SaleDate.Date;
        sale.CreatedAt = DateTime.Now;
        sale.GrandTotal = sale.SubTotal - sale.Discount + sale.DeliveryCharge;
        sale.PaidAmount = Math.Min(sale.PaidAmount, sale.GrandTotal);
        sale.DueAmount = sale.GrandTotal - sale.PaidAmount;
        sale.SoldBy = soldBy;
        sale.Payments = new();
        sale.InvoiceNo = "TEMP";

        // Customer: Id dile oi customer, na hole phone diye khuje / notun banay
        var name = sale.CustomerName?.Trim();
        var phone = sale.CustomerPhone?.Trim();
        Customer? customer = null;
        if (sale.CustomerId.HasValue)
        {
            customer = await _db.Customers.FindAsync(sale.CustomerId.Value);
            if (customer == null) return (null, "Customer pawa jay nai.");
        }
        else if (!string.IsNullOrEmpty(phone))
        {
            phone = PhoneHelper.Normalize(phone);
            if (phone == null) return (null, "Customer er phone number thik nai. 01XXXXXXXXX (11 digit) hote hobe.");

            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == phone)
                    ?? new Customer { Name = string.IsNullOrEmpty(name) ? "Customer" : name, Phone = phone };
        }

        if (sale.DueAmount > 0 && customer == null)
            return (null, "Baki rakhte customer er phone number dite hobe.");

        // Invoice no er jonno Id lage, tai duibar save. Baire transaction thakle sheta-i use hoy.
        var ownTx = _db.Database.CurrentTransaction == null ? await _db.Database.BeginTransactionAsync() : null;
        try
        {
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

            if (ownTx != null) await ownTx.CommitAsync();
        }
        finally
        {
            if (ownTx != null) await ownTx.DisposeAsync();
        }

        return (sale, null);
    }

    // Sale bad: stock ferot ashe, items / payments cascade e jay
    public async Task DeleteAsync(Sale sale)
    {
        var ids = sale.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        var variants = await _db.ProductVariants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id);

        foreach (var item in sale.Items)
            variants[item.ProductVariantId].Quantity += item.Quantity;

        _db.Sales.Remove(sale);
        await _db.SaveChangesAsync();
    }
}
