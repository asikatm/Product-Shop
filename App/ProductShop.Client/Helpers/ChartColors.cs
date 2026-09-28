namespace ProductShop.Client.Helpers;

// Chart er rong - validator diye check kora (CVD / normal-vision pass).
// Shob shomoy ei order e, kokhono ghuriye (cycle) na. 6 er beshi hole "Other".
public static class ChartColors
{
    public static readonly string[] Categorical = { "#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300" };

    // Status (paid / partial / due) - icon + label er sathe, rong ekla mane bohon kore na
    public const string Good = "#0ca30c";
    public const string Warning = "#fab219";
    public const string Critical = "#d03b3b";

    public const string Revenue = "#2a78d6";

    // Chart er grid / axis
    public const string Grid = "#eceef2";
    public const string Axis = "#c9ccd3";
    public const string Muted = "#898781";

    // "Other" shob shomoy dhushor
    public const string Other = "#b4b2aa";
}
