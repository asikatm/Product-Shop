using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductShop.Api.Data;
using ProductShop.Api.Services;
using ProductShop.Shared;

namespace ProductShop.Api.Controllers;

// Category ar Brand er CRUD same, tai ek base class
public abstract class NamedItemControllerBase<T> : ControllerBase where T : class, INamedItem
{
    protected readonly AppDbContext Db;

    protected NamedItemControllerBase(AppDbContext db)
    {
        Db = db;
    }

    protected abstract DbSet<T> Items { get; }

    // Kono product e use hole delete kora jabe na
    protected abstract Task<bool> IsUsedAsync(int id);

    [HttpGet]
    public async Task<ActionResult<List<T>>> GetAll()
    {
        return await Items.OrderBy(x => EF.Property<string>(x, "Name")).ToListAsync();
    }

    [HttpPost]
    [Permission(Perms.CatalogAdd)]
    public async Task<ActionResult<T>> Create(T item)
    {
        item.Id = 0;
        item.Name = item.Name.Trim();
        if (await NameTakenAsync(item.Name, 0))
            return BadRequest($"'{item.Name}' already ache.");

        Items.Add(item);
        await Db.SaveChangesAsync();
        return item;
    }

    [HttpPut("{id:int}")]
    [Permission(Perms.CatalogEdit)]
    public async Task<IActionResult> Update(int id, T item)
    {
        if (id != item.Id) return BadRequest("Id mismatch");

        var existing = await Items.FindAsync(id);
        if (existing == null) return NotFound();

        var name = item.Name.Trim();
        if (await NameTakenAsync(name, id))
            return BadRequest($"'{name}' already ache.");

        existing.Name = name;
        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Permission(Perms.CatalogDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await Items.FindAsync(id);
        if (existing == null) return NotFound();

        if (await IsUsedAsync(id))
            return BadRequest($"'{existing.Name}' e product ache, tai delete kora jabe na.");

        Items.Remove(existing);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    private Task<bool> NameTakenAsync(string name, int id) =>
        Items.AnyAsync(x => EF.Property<string>(x, "Name") == name && EF.Property<int>(x, "Id") != id);
}

[ApiController]
[Route("api/categories")]
public class CategoriesController : NamedItemControllerBase<Category>
{
    public CategoriesController(AppDbContext db) : base(db) { }

    protected override DbSet<Category> Items => Db.Categories;

    protected override Task<bool> IsUsedAsync(int id) => Db.Products.AnyAsync(p => p.CategoryId == id);
}

[ApiController]
[Route("api/brands")]
public class BrandsController : NamedItemControllerBase<Brand>
{
    public BrandsController(AppDbContext db) : base(db) { }

    protected override DbSet<Brand> Items => Db.Brands;

    protected override Task<bool> IsUsedAsync(int id) => Db.Products.AnyAsync(p => p.BrandId == id);
}

[ApiController]
[Route("api/shops")]
public class ShopsController : NamedItemControllerBase<Shop>
{
    public ShopsController(AppDbContext db) : base(db) { }

    protected override DbSet<Shop> Items => Db.Shops;

    protected override Task<bool> IsUsedAsync(int id) => Db.Products.AnyAsync(p => p.ShopId == id);
}

[ApiController]
[Route("api/suppliers")]
public class SuppliersController : NamedItemControllerBase<Supplier>
{
    public SuppliersController(AppDbContext db) : base(db) { }

    protected override DbSet<Supplier> Items => Db.Suppliers;

    protected override Task<bool> IsUsedAsync(int id) => Db.Products.AnyAsync(p => p.SupplierId == id);
}
