using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Api.Services;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly PermissionService _permissions;

    public AuthController(AppDbContext db, TokenService tokens, IPasswordHasher<AppUser> hasher, PermissionService permissions)
    {
        _db = db;
        _tokens = tokens;
        _hasher = hasher;
        _permissions = permissions;
    }

    // POST: api/auth/login
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var username = request.Username.Trim();
        var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstOrDefaultAsync(u => u.Username == username);

        if (user == null || _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return BadRequest("Username ba password vul.");
        if (!user.IsActive)
            return BadRequest(user.LastLoginAt == null
                ? "Apnar account ekhono approve hoy nai. Admin approve korle login korte parben."
                : "Ei user ke bondho (inactive) kora hoyeche. Admin er sathe jogajog korun.");

        user.LastLoginAt = DateTime.Now;
        await _db.SaveChangesAsync();

        var response = _tokens.Create(user);
        var access = await _permissions.GetAsync(user.Id);
        response.Permissions = access.Permissions.ToList();
        response.IsSuperAdmin = access.IsSuperAdmin;
        return response;
    }

    // GET: api/auth/me
    // App chalu howar somoy ekhonkar role / permission ane (admin bodlale page reload e notun ta pay)
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUser>> Me()
    {
        var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstOrDefaultAsync(u => u.Id == User.UserId());
        if (user == null || !user.IsActive) return Unauthorized();

        var access = await _permissions.GetAsync(user.Id);
        return new CurrentUser { User = user.ToInfo(), Permissions = access.Permissions.ToList(), IsSuperAdmin = access.IsSuperAdmin };
    }

    // POST: api/auth/register
    // Notun user Salesman hisebe toiri hoy, kintu inactive - admin approve korle login korte parbe
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var username = request.Username.Trim();
        if (await _db.Users.AnyAsync(u => u.Username == username))
            return BadRequest($"'{username}' username already ache, onno ekta din.");

        var phone = PhoneHelper.Normalize(request.Phone);
        if (phone == null)
            return BadRequest("Mobile number thik nai. 01XXXXXXXXX (11 digit) hote hobe.");
        if (await _db.Users.AnyAsync(u => u.Phone == phone))
            return BadRequest("Ei mobile number diye already account ache.");

        var user = new AppUser
        {
            Username = username,
            FullName = request.FullName.Trim(),
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            IsActive = false
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        // Default "Salesman" role (thakle); admin approve er somoy bodlate pare
        var salesman = await _db.AppRoles.FirstOrDefaultAsync(r => r.Name == Roles.Salesman);
        if (salesman != null) user.UserRoles.Add(new AppUserRole { RoleId = salesman.Id });

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // POST: api/auth/change-password
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await _db.Users.FindAsync(User.UserId());
        if (user == null) return Unauthorized();

        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest("Current password vul.");

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
