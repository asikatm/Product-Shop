using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProductShop.Api.Data;
using ProductShop.Shared;

namespace ProductShop.Api.Services;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public static SymmetricSecurityKey SigningKey(IConfiguration config) =>
        new(Encoding.UTF8.GetBytes(config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key appsettings e nai")));

    public LoginResponse Create(AppUser user)
    {
        var expires = DateTime.UtcNow.AddHours(_config.GetValue("Jwt:ExpireHours", 12));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.GivenName, user.FullName),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = expires,
            Issuer = _config["Jwt:Issuer"],
            Audience = _config["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(SigningKey(_config), SecurityAlgorithms.HmacSha256)
        };

        return new LoginResponse
        {
            Token = new JsonWebTokenHandler().CreateToken(descriptor),
            ExpiresAt = expires,
            User = user.ToInfo()
        };
    }
}

public static class ClaimsPrincipalExtensions
{
    public static int UserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Invoice e "Sold by" hisebe dekhano naam
    public static string DisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.GivenName) ?? user.Identity?.Name ?? "Unknown";
}
