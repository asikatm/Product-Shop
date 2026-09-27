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
    public string Role { get; set; } = Roles.Salesman;
    public bool IsActive { get; set; } = true;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginAt { get; set; }

    public UserInfo ToInfo() => new()
    {
        Id = Id,
        Username = Username,
        FullName = FullName,
        Email = Email,
        Phone = Phone,
        Role = Role,
        IsActive = IsActive,
        CreatedAt = CreatedAt,
        LastLoginAt = LastLoginAt
    };
}
