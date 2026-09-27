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
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = Roles.Salesman;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Register koreche kintu admin ekhono approve kore nai (kokhono login hoy nai)
    public bool IsPending => !IsActive && LastLoginAt == null;
}

// Login page theke notun account er request. Admin approve korle login kora jabe.
public class RegisterRequest
{
    [Required(ErrorMessage = "Full name dite hobe")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username dite hobe")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username 3-50 okkhor")]
    [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Username e shudhu English okkhor, number, . _ - deya jabe")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number dite hobe")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [OptionalEmail]
    [StringLength(100)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Password dite hobe")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password kompokkhe 6 okkhor")]
    public string Password { get; set; } = string.Empty;

    [Compare(nameof(Password), ErrorMessage = "Duita password mile nai")]
    public string ConfirmPassword { get; set; } = string.Empty;
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

    [OptionalEmail]
    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

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

// Khali ("") hole valid; kichu likhle thik email hote hobe
public class OptionalEmailAttribute : ValidationAttribute
{
    public OptionalEmailAttribute() : base("Email thik nai (jemon name@example.com)") { }

    public override bool IsValid(object? value) =>
        value is not string s || string.IsNullOrWhiteSpace(s) || new EmailAddressAttribute().IsValid(s.Trim());
}