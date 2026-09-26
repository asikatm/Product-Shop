using ProductShop.Shared;

namespace ProductShop.Client.Services;

// Ke login kore ache, sheta puro app e ek jaygay thake
public class AuthState
{
    public LoginResponse? Login { get; private set; }

    public UserInfo? User => Login?.User;

    public string? Token => Login?.Token;

    public bool IsLoggedIn => Login != null && Login.ExpiresAt > DateTime.UtcNow;

    public bool IsAdmin => IsLoggedIn && User!.Role == Roles.Admin;

    public event Action? Changed;

    public void Set(LoginResponse login)
    {
        Login = login;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (Login == null) return;
        Login = null;
        Changed?.Invoke();
    }
}
