using System.Net.Http.Json;
using ProductShop.Shared;

namespace ProductShop.Client.Services;

// Category ar Brand - duitar API same rokom, tai url diye alada kora
public class CatalogService
{
    public const string CategoriesUrl = "api/categories";
    public const string BrandsUrl = "api/brands";

    private readonly HttpClient _http;

    public CatalogService(HttpClient http)
    {
        _http = http;
    }

    public Task<List<Category>> GetCategoriesAsync() => GetAllAsync<Category>(CategoriesUrl);

    public Task<List<Brand>> GetBrandsAsync() => GetAllAsync<Brand>(BrandsUrl);

    public async Task<List<T>> GetAllAsync<T>(string url)
    {
        return await _http.GetFromJsonAsync<List<T>>(url) ?? new List<T>();
    }

    public async Task CreateAsync<T>(string url, T item) where T : INamedItem
    {
        var response = await _http.PostAsJsonAsync(url, item);
        await response.EnsureOkAsync();
    }

    public async Task UpdateAsync<T>(string url, T item) where T : INamedItem
    {
        var response = await _http.PutAsJsonAsync($"{url}/{item.Id}", item);
        await response.EnsureOkAsync();
    }

    public async Task DeleteAsync(string url, int id)
    {
        var response = await _http.DeleteAsync($"{url}/{id}");
        await response.EnsureOkAsync();
    }
}
