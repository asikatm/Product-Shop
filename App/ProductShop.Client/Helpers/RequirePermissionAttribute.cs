namespace ProductShop.Client.Helpers;

// Page e @attribute [RequirePermission(Perms.StockView)] dile oi permission chara page khulbe na (App.razor check kore).
// Ekadhik dile jekono ekta thaklei hobe.
[AttributeUsage(AttributeTargets.Class)]
public class RequirePermissionAttribute : Attribute
{
    public RequirePermissionAttribute(params string[] anyOf)
    {
        AnyOf = anyOf;
    }

    public string[] AnyOf { get; }
}
