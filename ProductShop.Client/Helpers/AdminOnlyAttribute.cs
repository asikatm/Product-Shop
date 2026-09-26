namespace ProductShop.Client.Helpers;

// Page e @attribute [AdminOnly] dile Salesman oi page dekhte parbe na (App.razor check kore)
[AttributeUsage(AttributeTargets.Class)]
public class AdminOnlyAttribute : Attribute
{
}
