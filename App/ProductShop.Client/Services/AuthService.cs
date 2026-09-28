using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using ProductShop.Shared;

namespace ProductShop.Client.Services;

public class AuthService
{
    // Browser er localStorage e login rakha hoy, jate refresh korle abar login na lage
    private const string StorageKey = "productshop.auth";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly AuthState _state;
    private readonly IJSRuntime _js;

    public AuthService(HttpClient http, AuthState state, IJSRuntime js)
    {
        _http = http;
        _state = state;
        _js = js;
    }

    public async Task InitializeAsync()
    {
        if (_state.IsLoggedIn) return;

        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(json)) return;

            var saved = JsonSerializer.Deserialize<LoginResponse>(json, JsonOptions);
            if (saved == null || saved.ExpiresAt <= DateTime.UtcNow)
            {
                await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
                return;
            }

            _state.Set(saved);
            await RefreshAsync();
        }
        catch (JsonException)
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
    }

    // Admin role / permission bodlale page reload e notun permission ashe.
    // User inactive / delete hole server 401 dey, AuthHandler logout kore.
    public async Task RefreshAsync()
    {
        try
        {
            var current = await _http.GetFromJsonAsync<CurrentUser>("api/auth/me");
            if (current == null) return;

            _state.Update(current);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(_state.Login, JsonOptions));
        }
        catch (HttpRequestException)
        {
            // API bondho thakle purono permission diye cholbe; 401 hole AuthHandler already logout koreche
        }
    }

    public async Task LoginAsync(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", request);
        await response.EnsureOkAsync();

        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(login, JsonOptions));
        _state.Set(login);
    }

    // Account request pathay; admin approve korle login kora jabe
    public async Task RegisterAsync(RegisterRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/register", request);
        await response.EnsureOkAsync();
    }

    public async Task LogoutAsync()
    {
        _state.Clear();
        await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/change-password", request);
        await response.EnsureOkAsync();
    }
}
