using System.Net.Http.Json;
using ProductShop.Shared;

namespace ProductShop.Client.Services;

public class StockService
{
    private readonly HttpClient _http;

    public StockService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<StockEntry>> GetAllAsync(DateTime? from, DateTime? to)
    {
        var url = ApiHelper.WithDateRange("api/stockentries", from, to);
        return await _http.GetFromJsonAsync<List<StockEntry>>(url) ?? new List<StockEntry>();
    }

    public async Task<StockEntry?> GetByIdAsync(int id)
    {
        return await _http.GetFromJsonAsync<StockEntry>($"api/stockentries/{id}");
    }

    public async Task<StockEntry> CreateAsync(StockEntry entry)
    {
        var response = await _http.PostAsJsonAsync("api/stockentries", entry);
        await response.EnsureOkAsync();
        return (await response.Content.ReadFromJsonAsync<StockEntry>())!;
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/stockentries/{id}");
        await response.EnsureOkAsync();
    }
}
