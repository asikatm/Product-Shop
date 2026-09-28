using System.Net.Http.Json;
using ProductShop.Shared;

namespace ProductShop.Client.Services;

public class RoleService
{
    private readonly HttpClient _http;

    public RoleService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<RoleInfo>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<RoleInfo>>("api/roles") ?? new List<RoleInfo>();
    }

    public async Task CreateAsync(RoleSaveRequest role)
    {
        var response = await _http.PostAsJsonAsync("api/roles", role);
        await response.EnsureOkAsync();
    }

    public async Task UpdateAsync(RoleSaveRequest role)
    {
        var response = await _http.PutAsJsonAsync($"api/roles/{role.Id}", role);
        await response.EnsureOkAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/roles/{id}");
        await response.EnsureOkAsync();
    }

    public async Task SetPermissionsAsync(int roleId, IEnumerable<string> permissions)
    {
        var response = await _http.PutAsJsonAsync($"api/roles/{roleId}/permissions", new RolePermissionsRequest { Permissions = permissions.ToList() });
        await response.EnsureOkAsync();
    }
}
