using System.Net.Http.Json;
using ProductShop.Shared;

namespace ProductShop.Client.Services;

// Website (public) er API - login lage na
public class StoreService
{
    private readonly HttpClient _http;

    public StoreService(HttpClient http)
    {
        _http = http;
    }

    public string ImageUrl(string url) => new Uri(_http.BaseAddress!, url).ToString();

    public async Task<StoreInfo> GetInfoAsync() =>
        await _http.GetFromJsonAsync<StoreInfo>("api/store/info") ?? new StoreInfo();

    public async Task<List<StoreCategory>> GetCategoriesAsync() =>
        await _http.GetFromJsonAsync<List<StoreCategory>>("api/store/categories") ?? new();

    public async Task<List<StoreProduct>> GetProductsAsync(int? categoryId = null, string? q = null)
    {
        var query = new List<string>();
        if (categoryId.HasValue) query.Add($"categoryId={categoryId}");
        if (!string.IsNullOrWhiteSpace(q)) query.Add($"q={Uri.EscapeDataString(q.Trim())}");
        var url = "api/store/products" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await _http.GetFromJsonAsync<List<StoreProduct>>(url) ?? new();
    }

    public async Task<StoreProduct?> GetProductAsync(int id)
    {
        var response = await _http.GetAsync($"api/store/products/{id}");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<StoreProduct>() : null;
    }

    public async Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/store/orders", request);
        if ((int)response.StatusCode == 429)
            throw new Exception("Onek beshi order hoye geche, 1 minute por abar chesta korun.");
        await response.EnsureOkAsync();
        return (await response.Content.ReadFromJsonAsync<PlaceOrderResponse>())!;
    }

    public async Task<OrderTrack?> TrackAsync(string orderNo, string phone)
    {
        var response = await _http.GetAsync($"api/store/orders/{Uri.EscapeDataString(orderNo)}?phone={Uri.EscapeDataString(phone)}");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<OrderTrack>() : null;
    }
}
