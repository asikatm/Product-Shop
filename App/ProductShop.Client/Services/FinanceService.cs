using System.Net.Http.Json;
using ProductShop.Shared;

namespace ProductShop.Client.Services;

// Finance menu er shob page: account, aay-bey, courier, asset, report
public class FinanceService
{
    private readonly HttpClient _http;

    public FinanceService(HttpClient http)
    {
        _http = http;
    }

    // ---- Accounts ----
    public async Task<List<FinanceAccount>> GetAccountsAsync() =>
        await _http.GetFromJsonAsync<List<FinanceAccount>>("api/finance/accounts") ?? new();

    public Task SaveAccountAsync(FinanceAccount a) => SaveAsync("api/finance/accounts", a.Id, a);

    public Task DeleteAccountAsync(int id) => DeleteAsync($"api/finance/accounts/{id}");

    // ---- Income / Expense / Transfer ----
    public async Task<List<FinanceTransaction>> GetTransactionsAsync(DateTime? from, DateTime? to, string? type = null, int? accountId = null)
    {
        var url = ApiHelper.WithDateRange("api/finance/transactions", from, to);
        if (!string.IsNullOrEmpty(type)) url += (url.Contains('?') ? "&" : "?") + $"type={Uri.EscapeDataString(type)}";
        if (accountId.HasValue) url += (url.Contains('?') ? "&" : "?") + $"accountId={accountId}";
        return await _http.GetFromJsonAsync<List<FinanceTransaction>>(url) ?? new();
    }

    public Task SaveTransactionAsync(FinanceTransaction t) => SaveAsync("api/finance/transactions", t.Id, t);

    public Task DeleteTransactionAsync(int id) => DeleteAsync($"api/finance/transactions/{id}");

    // ---- Courier ----
    public async Task<List<CourierSettlement>> GetSettlementsAsync() =>
        await _http.GetFromJsonAsync<List<CourierSettlement>>("api/finance/courier") ?? new();

    public async Task<List<PendingCourierOrder>> GetPendingCourierAsync() =>
        await _http.GetFromJsonAsync<List<PendingCourierOrder>>("api/finance/courier/pending") ?? new();

    public async Task<CourierSettlement> CreateSettlementAsync(CourierSettlementRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/finance/courier", request);
        await response.EnsureOkAsync();
        return (await response.Content.ReadFromJsonAsync<CourierSettlement>())!;
    }

    public Task DeleteSettlementAsync(int id) => DeleteAsync($"api/finance/courier/{id}");

    // ---- Assets ----
    public async Task<List<Asset>> GetAssetsAsync() =>
        await _http.GetFromJsonAsync<List<Asset>>("api/finance/assets") ?? new();

    public Task SaveAssetAsync(Asset a) => SaveAsync("api/finance/assets", a.Id, a);

    public Task DeleteAssetAsync(int id) => DeleteAsync($"api/finance/assets/{id}");

    // ---- Report ----
    public async Task<FinanceSummary> GetSummaryAsync(DateTime from, DateTime to) =>
        (await _http.GetFromJsonAsync<FinanceSummary>(ApiHelper.WithDateRange("api/finance/summary", from, to)))!;

    public async Task<CashFlowReport> GetCashFlowAsync(DateTime from, DateTime to, string group) =>
        (await _http.GetFromJsonAsync<CashFlowReport>(ApiHelper.WithDateRange("api/finance/cashflow", from, to) + $"&group={group}"))!;

    private async Task SaveAsync<T>(string url, int id, T body)
    {
        var response = id == 0 ? await _http.PostAsJsonAsync(url, body) : await _http.PutAsJsonAsync($"{url}/{id}", body);
        await response.EnsureOkAsync();
    }

    private async Task DeleteAsync(string url)
    {
        var response = await _http.DeleteAsync(url);
        await response.EnsureOkAsync();
    }
}
