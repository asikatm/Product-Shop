using ProductShop.Shared;

namespace ProductShop.Client.Helpers;

// Protiti menu ar permission er nijer rong (sidebar, menu permission page e same rong)
public static class MenuStyle
{
    private static readonly Dictionary<string, string> Tones = new()
    {
        [AppMenus.Dashboard] = "primary",
        [AppMenus.Sales] = "success",
        [AppMenus.WebOrders] = "orange",
        [AppMenus.Customers] = "pink",
        [AppMenus.Products] = "info",
        [AppMenus.Stock] = "orange",
        [AppMenus.Catalog] = "purple",
        [AppMenus.CostPrice] = "warning",
        [AppMenus.Users] = "teal",
        [AppMenus.Roles] = "danger"
    };

    private static readonly string[] RoleTones = { "primary", "success", "pink", "orange", "teal", "purple", "info", "warning" };

    public static string Tone(string menuKey) => Tones.GetValueOrDefault(menuKey, "primary");

    // Admin sob shomoy lal-beguni, baki role Id onujayi alada rong
    public static string RoleTone(RoleInfo role) => role.IsSystem ? "danger" : RoleTones[role.Id % RoleTones.Length];

    public static string ActionTone(string action) => action switch
    {
        PermAction.View => "view",
        PermAction.Add => "add",
        PermAction.Edit => "edit",
        _ => "delete"
    };

    public static string ActionIcon(string action) => action switch
    {
        PermAction.View => "bi-eye",
        PermAction.Add => "bi-plus-circle",
        PermAction.Edit => "bi-pencil-square",
        _ => "bi-trash3"
    };
}
