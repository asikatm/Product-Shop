using System.ComponentModel.DataAnnotations;

namespace ProductShop.Shared;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Salesman = "Salesman";

    public static readonly string[] All = { Admin, Salesman };
}

public class LoginRequest
{
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserInfo User { get; set; } = new();
}

public class UserInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Salesman;
    public bool IsActive { get; set; } = true;
}

public class UserSaveRequest
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Username is required")]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = Roles.Salesman;

    public bool IsActive { get; set; } = true;

    // Edit er somoy khali rakhle password change hobe na
    [StringLength(100, MinimumLength = 4, ErrorMessage = "Password kompokkhe 4 okkhor")]
    public string? Password { get; set; }
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Current password is required")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required")]
    [StringLength(100, MinimumLength = 4, ErrorMessage = "Password kompokkhe 4 okkhor")]
    public string NewPassword { get; set; } = string.Empty;
}
