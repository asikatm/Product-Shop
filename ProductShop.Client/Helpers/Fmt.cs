namespace ProductShop.Client.Helpers;

public static class Fmt
{
    public static string Money(decimal value) => "৳ " + value.ToString("N2");

    public static string Date(DateTime value) => value.ToString("dd-MMM-yyyy");
}
