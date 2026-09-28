using System.ComponentModel.DataAnnotations;

namespace ProductShop.Shared;

// Protiti menu te 4 rokom kaaj. Permission string: "menu.action", jemon "products.add"
public static class PermAction
{
    public const string View = "view";
    public const string Add = "add";
    public const string Edit = "edit";
    public const string Delete = "delete";

    public static readonly string[] All = { View, Add, Edit, Delete };

    public static string Key(string menu, string action) => $"{menu}.{action}";
}

// Code e use korar jonno permission gulo constant hisebe (attribute e lage)
public static class Perms
{
    public const string DashboardView = "dashboard.view";
    public const string SalesView = "sales.view";
    public const string SalesAdd = "sales.add";
    public const string SalesDelete = "sales.delete";
    public const string WebOrdersView = "weborders.view";
    public const string WebOrdersEdit = "weborders.edit";
    public const string CustomersView = "customers.view";
    public const string CustomersAdd = "customers.add";
    public const string CustomersEdit = "customers.edit";
    public const string CustomersDelete = "customers.delete";
    public const string ProductsView = "products.view";
    public const string ProductsAdd = "products.add";
    public const string ProductsEdit = "products.edit";
    public const string ProductsDelete = "products.delete";
    public const string StockView = "stock.view";
    public const string StockAdd = "stock.add";
    public const string StockDelete = "stock.delete";
    public const string CatalogView = "catalog.view";
    public const string CatalogAdd = "catalog.add";
    public const string CatalogEdit = "catalog.edit";
    public const string CatalogDelete = "catalog.delete";
    public const string CostView = "cost.view";
    public const string UsersView = "users.view";
    public const string UsersAdd = "users.add";
    public const string UsersEdit = "users.edit";
    public const string UsersDelete = "users.delete";
    public const string RolesView = "roles.view";
    public const string RolesAdd = "roles.add";
    public const string RolesEdit = "roles.edit";
    public const string RolesDelete = "roles.delete";

    // Notun Salesman role er default permission
    public static readonly string[] SalesmanDefaults =
        { DashboardView, SalesView, SalesAdd, CustomersView, CustomersAdd, CustomersEdit, ProductsView };
}

public record AppMenu(string Key, string Title, string Group, string Icon, string Description, params string[] Actions);

// App er shob menu. Menu Permission page e ei list ashe, API / UI eigulo diye check kore.
public static class AppMenus
{
    public const string Dashboard = "dashboard";
    public const string Sales = "sales";
    public const string WebOrders = "weborders";
    public const string Customers = "customers";
    public const string Products = "products";
    public const string Stock = "stock";
    public const string Catalog = "catalog";
    public const string CostPrice = "cost";
    public const string Users = "users";
    public const string Roles = "roles";

    public static readonly AppMenu[] All =
    {
        new(Dashboard, "Dashboard", "Main", "bi-grid-1x2", "Ajker sale, baki, stock", PermAction.View),
        new(Sales, "Sales", "Main", "bi-receipt", "Add = notun sale, Delete = sale bad", PermAction.View, PermAction.Add, PermAction.Delete),
        new(WebOrders, "Web Orders", "Main", "bi-globe2", "Website er order; Edit = confirm / deliver / cancel", PermAction.View, PermAction.Edit),
        new(Customers, "Customers", "Main", "bi-people", "Edit = baki joma newa", PermAction.View, PermAction.Add, PermAction.Edit, PermAction.Delete),
        new(Products, "Products", "Inventory", "bi-box-seam", "Product list, add / edit", PermAction.View, PermAction.Add, PermAction.Edit, PermAction.Delete),
        new(Stock, "Stock In", "Inventory", "bi-truck", "Supplier theke mal entry", PermAction.View, PermAction.Add, PermAction.Delete),
        new(Catalog, "Category / Brand", "Inventory", "bi-tags", "Category, brand, shop, supplier list", PermAction.View, PermAction.Add, PermAction.Edit, PermAction.Delete),
        new(CostPrice, "Kena dam & profit", "Inventory", "bi-eye", "Kena dam, profit, stock value dekha", PermAction.View),
        new(Users, "Users", "Administrative", "bi-person-gear", "User add, approve, role deya", PermAction.View, PermAction.Add, PermAction.Edit, PermAction.Delete),
        new(Roles, "Roles & Menu Permission", "Administrative", "bi-shield-lock", "Role banano ar permission deya", PermAction.View, PermAction.Add, PermAction.Edit, PermAction.Delete)
    };

    public static AppMenu? Find(string key) => All.FirstOrDefault(m => m.Key == key);

    // Shob valid permission string
    public static IEnumerable<string> AllKeys() => All.SelectMany(m => m.Actions.Select(a => PermAction.Key(m.Key, a)));
}

public class RoleInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Admin: shob permission, delete / edit kora jay na
    public bool IsSystem { get; set; }

    public int UserCount { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class RoleSaveRequest
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Role er naam dite hobe")]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }
}

public class RolePermissionsRequest
{
    public List<string> Permissions { get; set; } = new();
}
