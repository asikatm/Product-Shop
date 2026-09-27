using System.Net.Http.Json;
using ProductShop.Shared;

namespace ProductShop.Client.Services;

public class UserService
{
    private readonly HttpClient _http;

    public UserService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<UserInfo>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<UserInfo>>("api/users") ?? new List<UserInfo>();
    }

    public async Task CreateAsync(UserSaveRequest user)
    {
        var response = await _http.PostAsJsonAsync("api/users", user);
        await response.EnsureOkAsync();
    }

    public async Task UpdateAsync(UserSaveRequest user)
    {
        var response = await _http.PutAsJsonAsync($"api/users/{user.Id}", user);
        await response.EnsureOkAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/users/{id}");
        await response.EnsureOkAsync();
    }
}
