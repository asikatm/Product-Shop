using System.Net.Http.Json;
using ProductShop.Shared;

namespace ProductShop.Client.Services;

public class ProductService
{
    private readonly HttpClient _http;

    public ProductService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<Product>>("api/products") ?? new List<Product>();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _http.GetFromJsonAsync<Product>($"api/products/{id}");
    }

    public async Task CreateAsync(Product product)
    {
        var response = await _http.PostAsJsonAsync("api/products", product);
        await response.EnsureOkAsync();
    }

    public async Task UpdateAsync(Product product)
    {
        var response = await _http.PutAsJsonAsync($"api/products/{product.Id}", product);
        await response.EnsureOkAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/products/{id}");
        await response.EnsureOkAsync();
    }
}
