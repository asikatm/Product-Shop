using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Api.Services;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

// Account er balance: opening + income - expense - transfer out + transfer in
internal static class FinanceMath
{
    public static async Task<Dictionary<int, decimal>> BalancesAsync(AppDbContext db)
    {
        var accounts = await db.FinanceAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.OpeningBalance);

        var outs = await db.FinanceTransactions.GroupBy(t => new { t.AccountId, t.Type })
            .Select(g => new { g.Key.AccountId, g.Key.Type, Sum = g.Sum(t => t.Amount) }).ToListAsync();
        var ins = await db.FinanceTransactions.Where(t => t.Type == TxnTypes.Transfer && t.ToAccountId != null)
            .GroupBy(t => t.ToAccountId!.Value).Select(g => new { AccountId = g.Key, Sum = g.Sum(t => t.Amount) }).ToListAsync();

        foreach (var o in outs.Where(o => accounts.ContainsKey(o.AccountId)))
            accounts[o.AccountId] += o.Type == TxnTypes.Income ? o.Sum : -o.Sum;
        foreach (var i in ins.Where(i => accounts.ContainsKey(i.AccountId)))
            accounts[i.AccountId] += i.Sum;

        return accounts;
    }

    public static (DateTime From, DateTime To) Range(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        // Kind Unspecified - JSON e timezone offset jay na
        var f = DateTime.SpecifyKind((from ?? new DateTime(today.Year, today.Month, 1)).Date, DateTimeKind.Unspecified);
        var t = DateTime.SpecifyKind((to ?? today).Date, DateTimeKind.Unspecified);
        if (t < f) (f, t) = (t, f);
        return (f, t);
    }

    // Din onujayi taka ashlo / gelo - Cash Flow ar Finance Dashboard duitai eta use kore
    public static async Task<List<CashFlowRow>> DailyAsync(AppDbContext db, DateTime from, DateTime to)
    {
        var end = to.AddDays(1);

        // Sale er din paid (pore joma deya bade) + pore joma
        var atSale = await db.Sales.Where(s => s.SaleDate >= from && s.SaleDate < end)
            .Select(s => new { s.SaleDate, Amount = s.PaidAmount - s.Payments.Sum(p => p.Amount) }).ToListAsync();
        var later = await db.SalePayments.Where(p => p.PaymentDate >= from && p.PaymentDate < end)
            .Select(p => new { p.PaymentDate, p.Amount }).ToListAsync();
        var txns = await db.FinanceTransactions.Where(t => t.Date >= from && t.Date < end && t.Type != TxnTypes.Transfer)
            .Select(t => new { t.Date, t.Type, t.Source, t.Amount }).ToListAsync();
        var stock = await db.StockEntries.Where(s => s.EntryDate >= from && s.EntryDate < end)
            .Select(s => new { s.EntryDate, s.TotalAmount }).ToListAsync();

        var rows = new List<CashFlowRow>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var day = d;
            rows.Add(new CashFlowRow
            {
                Date = day,
                Label = day.ToString("dd MMM"),
                SalesCollection = atSale.Where(x => x.SaleDate.Date == day).Sum(x => x.Amount) + later.Where(x => x.PaymentDate.Date == day).Sum(x => x.Amount),
                // Courier er COD age-i sale collection e dhora hoyeche (deliver e), tai eikhane na
                OtherIncome = txns.Where(x => x.Date.Date == day && x.Type == TxnTypes.Income && x.Source != TxnSources.Courier).Sum(x => x.Amount),
                Expense = txns.Where(x => x.Date.Date == day && x.Type == TxnTypes.Expense).Sum(x => x.Amount),
                StockPurchase = stock.Where(x => x.EntryDate.Date == day).Sum(x => x.TotalAmount)
            });
        }
        return rows;
    }

    public static async Task<string?> AccountErrorAsync(AppDbContext db, int accountId, bool mustBeActive = true)
    {
        var account = await db.FinanceAccounts.FindAsync(accountId);
        if (account == null) return "Account pawa jay nai.";
        if (mustBeActive && !account.IsActive) return $"'{account.Name}' account bondho (inactive).";
        return null;
    }
}

// ---------------- Accounts & Bank ----------------
[ApiController]
[Route("api/finance/accounts")]
public class FinanceAccountsController : ControllerBase
{
    private readonly AppDbContext _db;

    public FinanceAccountsController(AppDbContext db)
    {
        _db = db;
    }

    // Finance er jekono page e account er dropdown lage
    [HttpGet]
    [Permission(Perms.AccountsView, Perms.FinanceView, Perms.TxnView, Perms.TxnAdd, Perms.CourierView, Perms.CourierAdd, Perms.CashFlowView, Perms.AssetsView, Perms.AssetsAdd)]
    public async Task<List<FinanceAccount>> GetAll()
    {
        var balances = await FinanceMath.BalancesAsync(_db);
        var list = await _db.FinanceAccounts.AsNoTracking().OrderByDescending(a => a.IsActive).ThenBy(a => a.Name).ToListAsync();
        foreach (var a in list) a.Balance = balances.GetValueOrDefault(a.Id);
        return list;
    }

    [HttpPost]
    [Permission(Perms.AccountsAdd)]
    public async Task<ActionResult<FinanceAccount>> Create(FinanceAccount account)
    {
        var error = await ValidateAsync(account, 0);
        if (error != null) return BadRequest(error);

        account.Id = 0;
        account.CreatedAt = DateTime.Now;
        _db.FinanceAccounts.Add(account);
        await _db.SaveChangesAsync();
        account.Balance = account.OpeningBalance;
        return account;
    }

    [HttpPut("{id:int}")]
    [Permission(Perms.AccountsEdit)]
    public async Task<IActionResult> Update(int id, FinanceAccount account)
    {
        var existing = await _db.FinanceAccounts.FindAsync(id);
        if (existing == null) return NotFound();

        var error = await ValidateAsync(account, id);
        if (error != null) return BadRequest(error);

        existing.Name = account.Name;
        existing.Type = account.Type;
        existing.BankName = account.BankName;
        existing.AccountNo = account.AccountNo;
        existing.Branch = account.Branch;
        existing.OpeningBalance = account.OpeningBalance;
        existing.IsActive = account.IsActive;
        existing.Note = account.Note;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Permission(Perms.AccountsDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _db.FinanceAccounts.FindAsync(id);
        if (existing == null) return NotFound();

        var used = await _db.FinanceTransactions.AnyAsync(t => t.AccountId == id || t.ToAccountId == id)
                || await _db.CourierSettlements.AnyAsync(s => s.AccountId == id)
                || await _db.Assets.AnyAsync(a => a.AccountId == id);
        if (used) return BadRequest($"'{existing.Name}' e lenden ache, tai delete kora jabe na. Inactive kore din.");

        _db.FinanceAccounts.Remove(existing);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string?> ValidateAsync(FinanceAccount account, int id)
    {
        account.Name = account.Name.Trim();
        if (account.Name.Length == 0) return "Account er naam dite hobe.";
        if (!AccountTypes.All.Contains(account.Type)) return "Account type thik nai.";
        if (await _db.FinanceAccounts.AnyAsync(a => a.Name == account.Name && a.Id != id)) return $"'{account.Name}' naame account already ache.";
        account.BankName = Clean(account.BankName);
        account.AccountNo = Clean(account.AccountNo);
        account.Branch = Clean(account.Branch);
        account.Note = Clean(account.Note);
        return null;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

// ---------------- Income & Expense (+ Transfer) ----------------
[ApiController]
[Route("api/finance/transactions")]
public class FinanceTransactionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public FinanceTransactionsController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/finance/transactions?from=&to=&type=&accountId=
    [HttpGet]
    [Permission(Perms.TxnView, Perms.AccountsView)]
    public async Task<List<FinanceTransaction>> GetAll(DateTime? from, DateTime? to, string? type, int? accountId)
    {
        var query = _db.FinanceTransactions.AsNoTracking().AsQueryable();
        if (from.HasValue) query = query.Where(t => t.Date >= from.Value.Date);
        if (to.HasValue) query = query.Where(t => t.Date < to.Value.Date.AddDays(1));
        if (!string.IsNullOrEmpty(type)) query = query.Where(t => t.Type == type);
        if (accountId.HasValue) query = query.Where(t => t.AccountId == accountId || t.ToAccountId == accountId);

        var list = await query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).Take(1000).ToListAsync();
        var names = await _db.FinanceAccounts.ToDictionaryAsync(a => a.Id, a => a.Name);
        foreach (var t in list)
        {
            t.AccountName = names.GetValueOrDefault(t.AccountId);
            t.ToAccountName = t.ToAccountId == null ? null : names.GetValueOrDefault(t.ToAccountId.Value);
        }
        return list;
    }

    [HttpPost]
    [Permission(Perms.TxnAdd)]
    public async Task<ActionResult<FinanceTransaction>> Create(FinanceTransaction txn)
    {
        var error = await ValidateAsync(txn, true);
        if (error != null) return BadRequest(error);

        txn.Id = 0;
        txn.Source = TxnSources.Manual;
        txn.CourierSettlementId = null;
        txn.AssetId = null;
        txn.CreatedBy = User.DisplayName();
        txn.CreatedAt = DateTime.Now;
        _db.FinanceTransactions.Add(txn);
        await _db.SaveChangesAsync();
        return txn;
    }

    [HttpPut("{id:int}")]
    [Permission(Perms.TxnEdit)]
    public async Task<IActionResult> Update(int id, FinanceTransaction txn)
    {
        var existing = await _db.FinanceTransactions.FindAsync(id);
        if (existing == null) return NotFound();
        if (existing.Source != TxnSources.Manual) return BadRequest($"Ei entry {existing.Source} page theke toiri, sekhan theke bodlan.");

        var error = await ValidateAsync(txn, existing.AccountId != txn.AccountId);
        if (error != null) return BadRequest(error);

        existing.Date = txn.Date;
        existing.Type = txn.Type;
        existing.AccountId = txn.AccountId;
        existing.ToAccountId = txn.ToAccountId;
        existing.Category = txn.Category;
        existing.Amount = txn.Amount;
        existing.Reference = txn.Reference;
        existing.Note = txn.Note;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Permission(Perms.TxnDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _db.FinanceTransactions.FindAsync(id);
        if (existing == null) return NotFound();
        if (existing.Source != TxnSources.Manual) return BadRequest($"Ei entry {existing.Source} page theke toiri, sekhan theke delete korun.");

        _db.FinanceTransactions.Remove(existing);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string?> ValidateAsync(FinanceTransaction txn, bool checkActive)
    {
        if (!TxnTypes.All.Contains(txn.Type)) return "Type thik nai.";
        if (txn.Amount <= 0) return "Taka 0 er beshi hote hobe.";
        var error = await FinanceMath.AccountErrorAsync(_db, txn.AccountId, checkActive);
        if (error != null) return error;

        txn.Date = txn.Date.Date;
        txn.Reference = string.IsNullOrWhiteSpace(txn.Reference) ? null : txn.Reference.Trim();
        txn.Note = string.IsNullOrWhiteSpace(txn.Note) ? null : txn.Note.Trim();

        if (txn.Type == TxnTypes.Transfer)
        {
            if (txn.ToAccountId == null) return "Kon account e transfer hobe select korun.";
            if (txn.ToAccountId == txn.AccountId) return "Same account e transfer kora jay na.";
            error = await FinanceMath.AccountErrorAsync(_db, txn.ToAccountId.Value);
            if (error != null) return error;
            txn.Category = "Transfer";
        }
        else
        {
            txn.ToAccountId = null;
            txn.Category = txn.Category?.Trim() ?? string.Empty;
            if (txn.Category.Length == 0) return "Category / khat dite hobe.";
        }
        return null;
    }
}

// ---------------- Courier Settlement ----------------
[ApiController]
[Route("api/finance/courier")]
public class CourierSettlementsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CourierSettlementsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Permission(Perms.CourierView)]
    public async Task<List<CourierSettlement>> GetAll()
    {
        var list = await _db.CourierSettlements.AsNoTracking().OrderByDescending(s => s.Date).ThenByDescending(s => s.Id).Take(500).ToListAsync();
        var names = await _db.FinanceAccounts.ToDictionaryAsync(a => a.Id, a => a.Name);
        var ids = list.Select(s => s.Id).ToList();
        var orders = await _db.WebOrders.Where(o => o.CourierSettlementId != null && ids.Contains(o.CourierSettlementId.Value))
            .Select(o => new { o.CourierSettlementId, o.OrderNo }).ToListAsync();
        foreach (var s in list)
        {
            s.AccountName = names.GetValueOrDefault(s.AccountId);
            s.OrderNos = orders.Where(o => o.CourierSettlementId == s.Id).Select(o => o.OrderNo).OrderBy(n => n).ToList();
        }
        return list;
    }

    // Deliver hoyeche kintu courier theke taka bujhe pawa hoy nai
    [HttpGet("pending")]
    [Permission(Perms.CourierView, Perms.CourierAdd, Perms.FinanceView)]
    public async Task<List<PendingCourierOrder>> Pending()
    {
        return await _db.WebOrders.AsNoTracking()
            .Where(o => o.Status == WebOrderStatus.Delivered && o.CourierSettlementId == null)
            .OrderBy(o => o.DeliveredAt ?? o.UpdatedAt)
            .Select(o => new PendingCourierOrder
            {
                Id = o.Id, OrderNo = o.OrderNo, CustomerName = o.CustomerName, Phone = o.Phone,
                Total = o.Total - o.Advance, DeliveryCharge = o.DeliveryCharge,
                DeliveredAt = o.DeliveredAt ?? o.UpdatedAt ?? o.CreatedAt
            })
            .ToListAsync();
    }

    [HttpPost]
    [Permission(Perms.CourierAdd)]
    public async Task<ActionResult<CourierSettlement>> Create(CourierSettlementRequest request)
    {
        var error = await FinanceMath.AccountErrorAsync(_db, request.AccountId);
        if (error != null) return BadRequest(error);

        var orders = new List<WebOrder>();
        if (request.WebOrderIds.Count > 0)
        {
            var ids = request.WebOrderIds.Distinct().ToList();
            orders = await _db.WebOrders.Where(o => ids.Contains(o.Id)).ToListAsync();
            if (orders.Count != ids.Count) return BadRequest("Kichu order pawa jay nai.");
            var bad = orders.FirstOrDefault(o => o.Status != WebOrderStatus.Delivered || o.CourierSettlementId != null);
            if (bad != null) return BadRequest($"{bad.OrderNo} deliver hoy nai ba age-i settle hoyeche.");
        }

        var count = orders.Count > 0 ? orders.Count : request.OrderCount;
        if (count <= 0) return BadRequest("Order select korun ba koyta order likhun.");
        if (request.CodAmount <= 0) return BadRequest("COD er taka 0 er beshi hote hobe.");
        var net = request.CodAmount - request.DeliveryCharge - request.CodCharge;
        if (net < 0) return BadRequest("Charge COD er cheye beshi hote parbe na.");

        var settlement = new CourierSettlement
        {
            Date = request.Date.Date,
            CourierName = request.CourierName.Trim(),
            AccountId = request.AccountId,
            ReferenceNo = string.IsNullOrWhiteSpace(request.ReferenceNo) ? null : request.ReferenceNo.Trim(),
            OrderCount = count,
            CodAmount = request.CodAmount,
            DeliveryCharge = request.DeliveryCharge,
            CodCharge = request.CodCharge,
            NetAmount = net,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedBy = User.DisplayName(),
            CreatedAt = DateTime.Now
        };

        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.CourierSettlements.Add(settlement);
        await _db.SaveChangesAsync();

        foreach (var o in orders) o.CourierSettlementId = settlement.Id;

        // Account e COD joma, courier er charge khoroch - dui mile account e net taka
        var note = $"{settlement.CourierName} · {count} ta order" + (settlement.ReferenceNo != null ? $" · {settlement.ReferenceNo}" : "");
        _db.FinanceTransactions.Add(Txn(settlement, TxnTypes.Income, "Courier COD", settlement.CodAmount, note));
        var fee = settlement.DeliveryCharge + settlement.CodCharge;
        if (fee > 0) _db.FinanceTransactions.Add(Txn(settlement, TxnTypes.Expense, "Courier charge", fee, note));

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        settlement.OrderNos = orders.Select(o => o.OrderNo).ToList();
        return settlement;
    }

    [HttpDelete("{id:int}")]
    [Permission(Perms.CourierDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _db.CourierSettlements.FindAsync(id);
        if (existing == null) return NotFound();

        // Order gulo abar "settle baki" hoy; transaction cascade e jay
        await _db.WebOrders.Where(o => o.CourierSettlementId == id).ExecuteUpdateAsync(s => s.SetProperty(o => o.CourierSettlementId, (int?)null));
        _db.CourierSettlements.Remove(existing);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private FinanceTransaction Txn(CourierSettlement s, string type, string category, decimal amount, string note) => new()
    {
        Date = s.Date, Type = type, AccountId = s.AccountId, Category = category, Amount = amount, Reference = s.ReferenceNo,
        Note = note, Source = TxnSources.Courier, CourierSettlementId = s.Id, CreatedBy = s.CreatedBy, CreatedAt = DateTime.Now
    };
}

// ---------------- Asset Management ----------------
[ApiController]
[Route("api/finance/assets")]
public class AssetsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AssetsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Permission(Perms.AssetsView)]
    public async Task<List<Asset>> GetAll() =>
        await _db.Assets.AsNoTracking().OrderBy(a => a.Status == AssetStatus.Disposed).ThenByDescending(a => a.PurchaseDate).ToListAsync();

    [HttpPost]
    [Permission(Perms.AssetsAdd)]
    public async Task<ActionResult<Asset>> Create(Asset asset)
    {
        var error = await ValidateAsync(asset);
        if (error != null) return BadRequest(error);

        asset.Id = 0;
        asset.CreatedAt = DateTime.Now;
        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        await SyncExpenseAsync(asset);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return asset;
    }

    [HttpPut("{id:int}")]
    [Permission(Perms.AssetsEdit)]
    public async Task<IActionResult> Update(int id, Asset asset)
    {
        var existing = await _db.Assets.FindAsync(id);
        if (existing == null) return NotFound();

        var error = await ValidateAsync(asset, existing.AccountId);
        if (error != null) return BadRequest(error);

        existing.Name = asset.Name;
        existing.Category = asset.Category;
        existing.PurchaseDate = asset.PurchaseDate;
        existing.PurchasePrice = asset.PurchasePrice;
        existing.Quantity = asset.Quantity;
        existing.DepreciationPercent = asset.DepreciationPercent;
        existing.Location = asset.Location;
        existing.Supplier = asset.Supplier;
        existing.Status = asset.Status;
        existing.AccountId = asset.AccountId;
        existing.Note = asset.Note;
        await SyncExpenseAsync(existing);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Permission(Perms.AssetsDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _db.Assets.FindAsync(id);
        if (existing == null) return NotFound();
        _db.Assets.Remove(existing);   // khoroch er entry cascade e jay
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Kon account theke kena - dile "Asset purchase" khoroch, na dile khoroch nai
    private async Task SyncExpenseAsync(Asset asset)
    {
        var txn = await _db.FinanceTransactions.FirstOrDefaultAsync(t => t.AssetId == asset.Id);
        if (asset.AccountId == null || asset.TotalCost <= 0)
        {
            if (txn != null) _db.FinanceTransactions.Remove(txn);
            return;
        }

        if (txn == null)
        {
            txn = new FinanceTransaction { Type = TxnTypes.Expense, Source = TxnSources.Asset, AssetId = asset.Id, Category = "Asset purchase", CreatedBy = User.DisplayName() };
            _db.FinanceTransactions.Add(txn);
        }
        txn.Date = asset.PurchaseDate.Date;
        txn.AccountId = asset.AccountId.Value;
        txn.Amount = asset.TotalCost;
        txn.Note = $"{asset.Name}" + (asset.Quantity > 1 ? $" × {asset.Quantity}" : "");
    }

    private async Task<string?> ValidateAsync(Asset asset, int? oldAccountId = null)
    {
        asset.Name = asset.Name.Trim();
        if (asset.Name.Length == 0) return "Asset er naam dite hobe.";
        if (asset.PurchasePrice < 0) return "Dam negative hote parbe na.";
        if (asset.Quantity < 1) return "Quantity kompokkhe 1.";
        if (asset.DepreciationPercent is < 0 or > 100) return "Depreciation 0 - 100 % er moddhe.";
        if (!AssetStatus.All.Contains(asset.Status)) return "Status thik nai.";
        if (asset.AccountId != null)
        {
            var error = await FinanceMath.AccountErrorAsync(_db, asset.AccountId.Value, asset.AccountId != oldAccountId);
            if (error != null) return error;
        }
        asset.PurchaseDate = asset.PurchaseDate.Date;
        asset.Category = string.IsNullOrWhiteSpace(asset.Category) ? "Others" : asset.Category.Trim();
        asset.Location = string.IsNullOrWhiteSpace(asset.Location) ? null : asset.Location.Trim();
        asset.Supplier = string.IsNullOrWhiteSpace(asset.Supplier) ? null : asset.Supplier.Trim();
        asset.Note = string.IsNullOrWhiteSpace(asset.Note) ? null : asset.Note.Trim();
        return null;
    }
}

// ---------------- Finance Dashboard + Cash Flow ----------------
[ApiController]
[Route("api/finance")]
public class FinanceReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PermissionService _perms;

    public FinanceReportsController(AppDbContext db, PermissionService perms)
    {
        _db = db;
        _perms = perms;
    }

    // GET: api/finance/summary?from=2026-09-01&to=2026-09-30
    [HttpGet("summary")]
    [Permission(Perms.FinanceView)]
    public async Task<FinanceSummary> Summary(DateTime? from, DateTime? to)
    {
        var (f, t) = FinanceMath.Range(from, to);
        var end = t.AddDays(1);
        var days = await FinanceMath.DailyAsync(_db, f, t);

        var balances = await FinanceMath.BalancesAsync(_db);
        var accounts = await _db.FinanceAccounts.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync();

        var expense = await _db.FinanceTransactions.Where(x => x.Type == TxnTypes.Expense && x.Date >= f && x.Date < end)
            .GroupBy(x => x.Category).Select(g => new NamedValue { Name = g.Key, Value = g.Sum(x => x.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Value).ToListAsync();
        if (expense.Count > 6)
            expense = expense.Take(5).Append(new NamedValue { Name = "Other", Value = expense.Skip(5).Sum(x => x.Value), Count = expense.Skip(5).Sum(x => x.Count) }).ToList();

        var pending = await _db.WebOrders.Where(o => o.Status == WebOrderStatus.Delivered && o.CourierSettlementId == null)
            .GroupBy(o => 1).Select(g => new { Count = g.Count(), Sum = g.Sum(o => o.Total - o.Advance) }).FirstOrDefaultAsync();

        var assets = await _db.Assets.AsNoTracking().ToListAsync();

        var summary = new FinanceSummary
        {
            From = f,
            To = t,
            TotalBalance = accounts.Sum(a => balances.GetValueOrDefault(a.Id)),
            SalesCollection = days.Sum(d => d.SalesCollection),
            OtherIncome = days.Sum(d => d.OtherIncome),
            Expense = days.Sum(d => d.Expense),
            StockPurchase = days.Sum(d => d.StockPurchase),
            CourierPendingOrders = pending?.Count ?? 0,
            CourierPendingAmount = pending?.Sum ?? 0,
            AssetValue = assets.Sum(a => a.CurrentValue),
            AccountBalances = accounts.Select(a => new NamedValue { Name = a.Name, Value = balances.GetValueOrDefault(a.Id) }).ToList(),
            ExpenseByCategory = expense,
            Trend = Bucket(days, t - f > TimeSpan.FromDays(62))
        };

        // Profit = (bikri dam - kena dam) * qty - discount; kena dam dekhar permission lage
        if (await _perms.HasAsync(User, Perms.CostView))
        {
            var sales = _db.Sales.Where(s => s.SaleDate >= f && s.SaleDate < end);
            var gross = await _db.SaleItems.Where(i => sales.Any(s => s.Id == i.SaleId)).SumAsync(i => (i.UnitPrice - i.CostPrice) * i.Quantity);
            summary.GrossProfit = gross - await sales.SumAsync(s => s.Discount);
        }

        var recent = await _db.FinanceTransactions.AsNoTracking().OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).Take(8).ToListAsync();
        var names = await _db.FinanceAccounts.ToDictionaryAsync(a => a.Id, a => a.Name);
        foreach (var r in recent)
        {
            r.AccountName = names.GetValueOrDefault(r.AccountId);
            r.ToAccountName = r.ToAccountId == null ? null : names.GetValueOrDefault(r.ToAccountId.Value);
        }
        summary.Recent = recent;
        return summary;
    }

    // GET: api/finance/cashflow?from=&to=&group=day|month
    [HttpGet("cashflow")]
    [Permission(Perms.CashFlowView)]
    public async Task<CashFlowReport> CashFlow(DateTime? from, DateTime? to, string? group)
    {
        var (f, t) = FinanceMath.Range(from, to);
        if ((t - f).TotalDays > 731) f = t.AddDays(-731);   // 2 bochorer beshi na
        var byMonth = group == "month";
        var rows = Bucket(await FinanceMath.DailyAsync(_db, f, t), byMonth);

        decimal running = 0;
        foreach (var r in rows)
        {
            running += r.Net;
            r.Running = running;
        }
        return new CashFlowReport { From = f, To = t, GroupBy = byMonth ? "month" : "day", Rows = rows };
    }

    private static List<CashFlowRow> Bucket(List<CashFlowRow> days, bool byMonth)
    {
        if (!byMonth) return days;
        return days.GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, 1)).Select(g => new CashFlowRow
        {
            Date = g.Key,
            Label = g.Key.ToString("MMM yyyy"),
            SalesCollection = g.Sum(x => x.SalesCollection),
            OtherIncome = g.Sum(x => x.OtherIncome),
            Expense = g.Sum(x => x.Expense),
            StockPurchase = g.Sum(x => x.StockPurchase)
        }).ToList();
    }
}
