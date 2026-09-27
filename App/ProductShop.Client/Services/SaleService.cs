using System.Net.Http.Json;
using ProductShop.Shared;

namespace ProductShop.Client.Services;

public class SaleService
{
    private readonly HttpClient _http;

    public SaleService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Sale>> GetAllAsync(DateTime? from, DateTime? to)
    {
        var url = ApiHelper.WithDateRange("api/sales", from, to);
        return await _http.GetFromJsonAsync<List<Sale>>(url) ?? new List<Sale>();
    }

    public async Task<Sale?> GetByIdAsync(int id)
    {
        return await _http.GetFromJsonAsync<Sale>($"api/sales/{id}");
    }

    public async Task<Sale> CreateAsync(Sale sale)
    {
        var response = await _http.PostAsJsonAsync("api/sales", sale);
        await response.EnsureOkAsync();
        return (await response.Content.ReadFromJsonAsync<Sale>())!;
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/sales/{id}");
        await response.EnsureOkAsync();
    }

    public async Task<DashboardSummary> GetDashboardAsync()
    {
        return await _http.GetFromJsonAsync<DashboardSummary>("api/dashboard") ?? new DashboardSummary();
    }
}
