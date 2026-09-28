using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ProductShop.Api.Data;
using ProductShop.Shared;

namespace ProductShop.Api.Services;

public record UserAccess(bool Exists, bool IsActive, bool IsSuperAdmin, HashSet<string> Permissions)
{
    public bool Has(string permission) => IsSuperAdmin || Permissions.Contains(permission);
}

// User er role theke permission ber kore. Protiti request e DB te na jaoar jonno 5 min cache;
// role / permission / user change hole Invalidate() diye cache fele deya hoy, tai change sathe sathe kaaj kore.
public class PermissionService
{
    private static int _version;

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public PermissionService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public static void Invalidate() => Interlocked.Increment(ref _version);

    public async Task<UserAccess> GetAsync(int userId)
    {
        var key = $"perm:{Volatile.Read(ref _version)}:{userId}";
        return (await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await LoadAsync(userId);
        }))!;
    }

    public async Task<bool> HasAsync(ClaimsPrincipal user, string permission) =>
        (await GetAsync(user.UserId())).Has(permission);

    private async Task<UserAccess> LoadAsync(int userId)
    {
        var active = await _db.Users.Where(u => u.Id == userId).Select(u => (bool?)u.IsActive).FirstOrDefaultAsync();
        if (active == null) return new UserAccess(false, false, false, new());

        var roles = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => new { ur.RoleId, ur.Role!.IsSystem })
            .ToListAsync();
        var roleIds = roles.Select(r => r.RoleId).ToList();
        var permissions = await _db.RolePermissions
            .Where(p => roleIds.Contains(p.RoleId))
            .Select(p => p.Permission)
            .Distinct()
            .ToListAsync();

        return new UserAccess(true, active.Value, roles.Any(r => r.IsSystem), permissions.ToHashSet());
    }
}

// [Permission(Perms.ProductsAdd)] - ekadhik dile jekono ekta thakleo cholbe.
// Controller ar action duitay dile duitai lagbe.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _anyOf;

    public PermissionAttribute(params string[] anyOf)
    {
        _anyOf = anyOf;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var service = context.HttpContext.RequestServices.GetRequiredService<PermissionService>();
        var access = await service.GetAsync(context.HttpContext.User.UserId());
        if (!_anyOf.Any(access.Has))
            context.Result = new ObjectResult("Apnar ei kaajer permission nai.") { StatusCode = StatusCodes.Status403Forbidden };
    }
}

// Shob API request e: user delete / inactive hole token thakleo ar kaaj korbe na
public class ActiveUserFilter : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) return;
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) return;

        var service = context.HttpContext.RequestServices.GetRequiredService<PermissionService>();
        var access = await service.GetAsync(context.HttpContext.User.UserId());
        if (!access.Exists || !access.IsActive)
            context.Result = new UnauthorizedResult();
    }
}
