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

    // Chobi upload kore relative url ferot dey
    public async Task<string> UploadImageAsync(Stream stream, string fileName, string contentType)
    {
        using var content = new MultipartFormDataContent();
        var file = new StreamContent(stream);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);

        var response = await _http.PostAsync("api/products/images", content);
        await response.EnsureOkAsync();
        var image = await response.Content.ReadFromJsonAsync<ProductImage>();
        return image!.Url;
    }

    // Chobir puro address (API server theke ashe)
    public string ImageUrl(string url) => new Uri(_http.BaseAddress!, url).ToString();

    public async Task DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/products/{id}");
        await response.EnsureOkAsync();
    }
}
