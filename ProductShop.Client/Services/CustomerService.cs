using System.Net.Http.Json;
using ProductShop.Shared;

namespace ProductShop.Client.Services;

public class CustomerService
{
    private readonly HttpClient _http;

    public CustomerService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Customer>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<Customer>>("api/customers") ?? new List<Customer>();
    }

    public async Task<CustomerDetails?> GetByIdAsync(int id)
    {
        return await _http.GetFromJsonAsync<CustomerDetails>($"api/customers/{id}");
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        var response = await _http.PostAsJsonAsync("api/customers", customer);
        await response.EnsureOkAsync();
        return (await response.Content.ReadFromJsonAsync<Customer>())!;
    }

    public async Task UpdateAsync(Customer customer)
    {
        var response = await _http.PutAsJsonAsync($"api/customers/{customer.Id}", customer);
        await response.EnsureOkAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/customers/{id}");
        await response.EnsureOkAsync();
    }

    public async Task ReceivePaymentAsync(int customerId, ReceivePaymentRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/customers/{customerId}/payments", request);
        await response.EnsureOkAsync();
    }

    public async Task DeletePaymentAsync(int paymentId)
    {
        var response = await _http.DeleteAsync($"api/customers/payments/{paymentId}");
        await response.EnsureOkAsync();
    }
}
