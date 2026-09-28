using ProductShop.Shared;

namespace ProductShop.Client.Services;

// Ke login kore ache ar ki ki korte pare, sheta puro app e ek jaygay thake
public class AuthState
{
    private HashSet<string> _permissions = new();

    public LoginResponse? Login { get; private set; }

    public UserInfo? User => Login?.User;

    public string? Token => Login?.Token;

    public bool IsLoggedIn => Login != null && Login.ExpiresAt > DateTime.UtcNow;

    // Admin (system) role: shob permission
    public bool IsSuperAdmin => IsLoggedIn && Login!.IsSuperAdmin;

    public string RoleText => User == null || User.Roles.Count == 0 ? "No role" : string.Join(", ", User.Roles);

    public event Action? Changed;

    // Jemon Auth.Can(Perms.ProductsAdd). Ekadhik dile jekono ekta thaklei true.
    public bool Can(params string[] anyOf) => IsLoggedIn && (Login!.IsSuperAdmin || anyOf.Any(_permissions.Contains));

    public void Set(LoginResponse login)
    {
        Login = login;
        _permissions = login.Permissions.ToHashSet();
        Changed?.Invoke();
    }

    // Server theke notun role / permission ashle (api/auth/me)
    public void Update(CurrentUser current)
    {
        if (Login == null) return;
        Login.User = current.User;
        Login.Permissions = current.Permissions;
        Login.IsSuperAdmin = current.IsSuperAdmin;
        _permissions = current.Permissions.ToHashSet();
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (Login == null) return;
        Login = null;
        _permissions = new();
        Changed?.Invoke();
    }
}
