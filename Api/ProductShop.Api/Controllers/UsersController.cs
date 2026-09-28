using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Api.Services;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;

    public UsersController(AppDbContext db, IPasswordHasher<AppUser> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    private IQueryable<AppUser> WithRoles => _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role);

    // GET: api/users
    [HttpGet]
    [Permission(Perms.UsersView)]
    public async Task<ActionResult<List<UserInfo>>> GetAll()
    {
        // Approve er opekkhay thaka user age
        var users = await WithRoles
            .OrderBy(u => u.IsActive || u.LastLoginAt != null)
            .ThenBy(u => u.Username)
            .ToListAsync();
        return users.Select(u => u.ToInfo()).ToList();
    }

    // POST: api/users
    [HttpPost]
    [Permission(Perms.UsersAdd)]
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
            Email = request.Email,
            Phone = request.Phone,
            IsActive = request.IsActive,
            UserRoles = request.RoleIds.Distinct().Select(id => new AppUserRole { RoleId = id }).ToList()
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        PermissionService.Invalidate();

        var created = await WithRoles.FirstAsync(u => u.Id == user.Id);
        return created.ToInfo();
    }

    // PUT: api/users/5
    [HttpPut("{id:int}")]
    [Permission(Perms.UsersEdit)]
    public async Task<IActionResult> Update(int id, UserSaveRequest request)
    {
        if (id != request.Id) return BadRequest("Id mismatch");

        var user = await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var error = await ValidateAsync(request, id);
        if (error != null) return BadRequest(error);

        var newRoleIds = request.RoleIds.Distinct().ToHashSet();

        // Nijeke bondho korle ba nijer role bodlale nije-i atke jete paren
        if (id == User.UserId())
        {
            if (!request.IsActive) return BadRequest("Nijeke inactive kora jabe na.");
            if (!newRoleIds.SetEquals(user.UserRoles.Select(r => r.RoleId)))
                return BadRequest("Nijer role nije bodlano jabe na. Onno ekjon admin ke bolun.");
        }

        user.Username = request.Username.Trim();
        user.FullName = request.FullName.Trim();
        user.Email = request.Email;
        user.Phone = request.Phone;
        user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = _hasher.HashPassword(user, request.Password);

        user.UserRoles.RemoveAll(r => !newRoleIds.Contains(r.RoleId));
        foreach (var roleId in newRoleIds.Where(rid => user.UserRoles.All(r => r.RoleId != rid)))
            user.UserRoles.Add(new AppUserRole { UserId = id, RoleId = roleId });

        if (!await HasActiveAdminAfterAsync(user))
            return BadRequest("Kompokkhe ekjon active Admin thakte hobe.");

        await _db.SaveChangesAsync();
        PermissionService.Invalidate();
        return NoContent();
    }

    // DELETE: api/users/5
    // Shudhu je kokhono login kore nai (jemon register request reject)
    [HttpDelete("{id:int}")]
    [Permission(Perms.UsersDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (id == User.UserId()) return BadRequest("Nijeke delete kora jabe na.");
        if (user.LastLoginAt != null)
            return BadRequest($"'{user.Username}' age login koreche, tai delete na kore inactive korun.");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        PermissionService.Invalidate();
        return NoContent();
    }

    // Ei change er por o kono active user er kache system (Admin) role thakbe kina
    private async Task<bool> HasActiveAdminAfterAsync(AppUser changed)
    {
        var systemRoleIds = await _db.AppRoles.Where(r => r.IsSystem).Select(r => r.Id).ToListAsync();
        if (changed.IsActive && changed.UserRoles.Any(r => systemRoleIds.Contains(r.RoleId))) return true;

        return await _db.Users.AnyAsync(u => u.Id != changed.Id && u.IsActive
            && _db.UserRoles.Any(ur => ur.UserId == u.Id && systemRoleIds.Contains(ur.RoleId)));
    }

    private async Task<string?> ValidateAsync(UserSaveRequest request, int id)
    {
        var roleIds = request.RoleIds.Distinct().ToList();
        if (await _db.AppRoles.CountAsync(r => roleIds.Contains(r.Id)) != roleIds.Count)
            return "Kichu role pawa jay nai.";

        var username = request.Username.Trim();
        if (await _db.Users.AnyAsync(u => u.Username == username && u.Id != id))
            return $"'{username}' username already ache.";

        request.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(request.Phone))
            request.Phone = null;
        else
        {
            request.Phone = PhoneHelper.Normalize(request.Phone);
            if (request.Phone == null) return "Mobile number thik nai. 01XXXXXXXXX (11 digit) hote hobe.";
        }

        return null;
    }
}
