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
[Authorize(Roles = Roles.Admin)]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;

    public UsersController(AppDbContext db, IPasswordHasher<AppUser> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    // GET: api/users
    [HttpGet]
    public async Task<ActionResult<List<UserInfo>>> GetAll()
    {
        var users = await _db.Users.OrderBy(u => u.Username).ToListAsync();
        return users.Select(u => u.ToInfo()).ToList();
    }

    // POST: api/users
    [HttpPost]
    public async Task<ActionResult<UserInfo>> Create(UserSaveRequest request)
    {
        var error = await ValidateAsync(request, 0);
        if (error != null) return BadRequest(error);
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Notun user er password dite hobe.");

        var user = new AppUser
        {
            Username = request.Username.Trim(),
            FullName = request.FullName.Trim(),
            Role = request.Role,
            IsActive = request.IsActive
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user.ToInfo();
    }

    // PUT: api/users/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UserSaveRequest request)
    {
        if (id != request.Id) return BadRequest("Id mismatch");

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        var error = await ValidateAsync(request, id);
        if (error != null) return BadRequest(error);

        // Nijeke admin theke shoriye dile ba bondho korle ar keu admin thakbe na
        if (id == User.UserId() && (request.Role != Roles.Admin || !request.IsActive))
            return BadRequest("Nijer admin role ba active status change kora jabe na.");

        user.Username = request.Username.Trim();
        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = _hasher.HashPassword(user, request.Password);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string?> ValidateAsync(UserSaveRequest request, int id)
    {
        if (!Roles.All.Contains(request.Role))
            return "Role thik nai.";

        var username = request.Username.Trim();
        if (await _db.Users.AnyAsync(u => u.Username == username && u.Id != id))
            return $"'{username}' username already ache.";

        return null;
    }
}
