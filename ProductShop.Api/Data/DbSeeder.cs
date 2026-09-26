using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProductShop.Shared;

namespace ProductShop.Api.Data;

public static class DbSeeder
{
    // Prothom bar chalale default admin ar kichu category toiri hoy
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<AppUser> hasher)
    {
        if (!await db.Users.AnyAsync())
        {
            var admin = new AppUser { Username = "admin", FullName = "Administrator", Role = Roles.Admin };
            admin.PasswordHash = hasher.HashPassword(admin, "admin123");
            db.Users.Add(admin);
        }

        if (!await db.Categories.AnyAsync())
        {
            var names = new[] { "Shirt", "T-Shirt", "Polo", "Pant", "Jeans", "Panjabi", "Saree", "Three Piece", "Kids" };
            db.Categories.AddRange(names.Select(n => new Category { Name = n }));
        }

        await db.SaveChangesAsync();
    }
}
