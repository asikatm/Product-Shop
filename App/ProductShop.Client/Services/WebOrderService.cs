using System.Net.Http.Json;
using ProductShop.Shared;

namespace ProductShop.Client.Services;

// Software er Web Orders page er jonno
public class WebOrderService
{
    private readonly HttpClient _http;

    public WebOrderService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<WebOrder>> GetAllAsync(string? status) =>
        await _http.GetFromJsonAsync<List<WebOrder>>("api/weborders" + (string.IsNullOrEmpty(status) ? "" : $"?status={status}")) ?? new();

    public async Task<Dictionary<string, int>> GetCountsAsync() =>
        await _http.GetFromJsonAsync<Dictionary<string, int>>("api/weborders/counts") ?? new();

    public async Task<WebOrder?> GetByIdAsync(int id) =>
        await _http.GetFromJsonAsync<WebOrder>($"api/weborders/{id}");

    public Task<WebOrder> ConfirmAsync(int id) => ActionAsync(id, "confirm", new { });

    public Task<WebOrder> DeliverAsync(int id) => ActionAsync(id, "deliver", new { });

    public Task<WebOrder> CancelAsync(int id, string? reason) => ActionAsync(id, "cancel", new CancelWebOrderRequest { Reason = reason });

    private async Task<WebOrder> ActionAsync(int id, string action, object body)
    {
        var response = await _http.PostAsJsonAsync($"api/weborders/{id}/{action}", body);
        await response.EnsureOkAsync();
        return (await response.Content.ReadFromJsonAsync<WebOrder>())!;
    }
}
