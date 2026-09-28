using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Api.Services;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly AppDbContext _db;

    public RolesController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/roles
    // User form e role list lage, tai users.view thakleo dekha jay
    [HttpGet]
    [Permission(Perms.RolesView, Perms.UsersView)]
    public async Task<ActionResult<List<RoleInfo>>> GetAll()
    {
        var roles = await _db.AppRoles
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .Select(r => new RoleInfo
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                IsSystem = r.IsSystem,
                UserCount = r.UserRoles.Count,
                Permissions = r.Permissions.Select(p => p.Permission).ToList()
            })
            .ToListAsync();

        // Admin er shob permission
        foreach (var role in roles.Where(r => r.IsSystem))
            role.Permissions = AppMenus.AllKeys().ToList();
        return roles;
    }

    // POST: api/roles
    [HttpPost]
    [Permission(Perms.RolesAdd)]
    public async Task<ActionResult<RoleInfo>> Create(RoleSaveRequest request)
    {
        var name = request.Name.Trim();
        if (await _db.AppRoles.AnyAsync(r => r.Name == name))
            return BadRequest($"'{name}' role already ache.");

        var role = new AppRole { Name = name, Description = Clean(request.Description) };
        _db.AppRoles.Add(role);
        await _db.SaveChangesAsync();
        return new RoleInfo { Id = role.Id, Name = role.Name, Description = role.Description };
    }

    // PUT: api/roles/5
    [HttpPut("{id:int}")]
    [Permission(Perms.RolesEdit)]
    public async Task<IActionResult> Update(int id, RoleSaveRequest request)
    {
        if (id != request.Id) return BadRequest("Id mismatch");

        var role = await _db.AppRoles.FindAsync(id);
        if (role == null) return NotFound();

        var name = request.Name.Trim();
        if (role.IsSystem && name != role.Name) return BadRequest("Admin role er naam bodlano jabe na.");
        if (await _db.AppRoles.AnyAsync(r => r.Name == name && r.Id != id))
            return BadRequest($"'{name}' role already ache.");

        role.Name = name;
        role.Description = Clean(request.Description);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PUT: api/roles/5/permissions
    // Menu Permission page theke: ei role er puro permission list replace hoy
    [HttpPut("{id:int}/permissions")]
    [Permission(Perms.RolesEdit)]
    public async Task<IActionResult> SetPermissions(int id, RolePermissionsRequest request)
    {
        var role = await _db.AppRoles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound();
        if (role.IsSystem) return BadRequest("Admin role er shob permission shob shomoy thake, bodlano jay na.");

        var valid = AppMenus.AllKeys().ToHashSet();
        var wanted = request.Permissions.Where(valid.Contains).ToHashSet();

        // Add / edit / delete thakle view o lagbe
        foreach (var p in wanted.ToList())
        {
            var menu = p[..p.IndexOf('.')];
            wanted.Add(PermAction.Key(menu, PermAction.View));
        }

        role.Permissions.RemoveAll(p => !wanted.Contains(p.Permission));
        foreach (var p in wanted.Where(w => role.Permissions.All(x => x.Permission != w)))
            role.Permissions.Add(new RolePermission { RoleId = id, Permission = p });

        await _db.SaveChangesAsync();
        PermissionService.Invalidate();
        return NoContent();
    }

    // DELETE: api/roles/5
    [HttpDelete("{id:int}")]
    [Permission(Perms.RolesDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _db.AppRoles.FindAsync(id);
        if (role == null) return NotFound();
        if (role.IsSystem) return BadRequest("Admin role delete kora jabe na.");

        var users = await _db.UserRoles.CountAsync(ur => ur.RoleId == id);
        if (users > 0) return BadRequest($"'{role.Name}' role e {users} jon user ache. Age tader onno role din.");

        _db.AppRoles.Remove(role);
        await _db.SaveChangesAsync();
        PermissionService.Invalidate();
        return NoContent();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
