using ProductShop.Shared;

namespace ProductShop.Api.Data;

// Password hash client e jay na, tai ei class Shared e rakha hoy nai
public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginAt { get; set; }

    public List<AppUserRole> UserRoles { get; set; } = new();

    // UserRoles -> Role Include kora thakle role er naam o ashe
    public UserInfo ToInfo() => new()
    {
        Id = Id,
        Username = Username,
        FullName = FullName,
        Email = Email,
        Phone = Phone,
        Roles = UserRoles.Where(r => r.Role != null).Select(r => r.Role!.Name).OrderBy(n => n).ToList(),
        RoleIds = UserRoles.Select(r => r.RoleId).ToList(),
        IsActive = IsActive,
        CreatedAt = CreatedAt,
        LastLoginAt = LastLoginAt
    };
}

public class AppRole
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Admin: shob permission, naam bodlano / delete kora jay na
    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<RolePermission> Permissions { get; set; } = new();
    public List<AppUserRole> UserRoles { get; set; } = new();
}

// Kon user er kon role
public class AppUserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public AppRole? Role { get; set; }
}

// Kon role kon menu te ki korte parbe, jemon "products.add"
public class RolePermission
{
    public int RoleId { get; set; }
    public string Permission { get; set; } = string.Empty;
}
