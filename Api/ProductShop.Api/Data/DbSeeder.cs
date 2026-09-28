using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProductShop.Shared;

namespace ProductShop.Api.Data;

public static class DbSeeder
{
    // Prothom bar chalale default role, admin ar kichu category toiri hoy
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<AppUser> hasher)
    {
        var adminRole = await db.AppRoles.FirstOrDefaultAsync(r => r.IsSystem);
        if (adminRole == null)
        {
            adminRole = new AppRole { Name = Roles.Admin, Description = "Shob kichu korte pare", IsSystem = true };
            db.AppRoles.Add(adminRole);
        }

        if (!await db.AppRoles.AnyAsync(r => r.Name == Roles.Salesman))
        {
            db.AppRoles.Add(new AppRole
            {
                Name = Roles.Salesman,
                Description = "Sale, customer ar baki joma",
                Permissions = Perms.SalesmanDefaults.Select(p => new RolePermission { Permission = p }).ToList()
            });
        }

        if (!await db.Users.AnyAsync())
        {
            var admin = new AppUser { Username = "admin", FullName = "Administrator" };
            admin.PasswordHash = hasher.HashPassword(admin, "admin123");
            admin.UserRoles.Add(new AppUserRole { Role = adminRole });
            db.Users.Add(admin);
        }

        if (!await db.Categories.AnyAsync())
        {
            var names = new[] { "Shirt", "T-Shirt", "Polo", "Pant", "Jeans", "Panjabi", "Saree", "Three Piece", "Kids" };
            db.Categories.AddRange(names.Select(n => new Category { Name = n }));
        }

        if (!await db.Shops.AnyAsync())
            db.Shops.Add(new Shop { Name = "Main shop" });

        await db.SaveChangesAsync();
    }
}
