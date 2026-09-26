namespace ProductShop.Client.Services;

public static class ApiHelper
{
    // API jodi error message pathay (jemon "stock nai"), seta exception message hisebe dekhabe
    public static async Task EnsureOkAsync(this HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var message = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(message))
            message = $"Server error ({(int)response.StatusCode})";

        throw new Exception(message);
    }

    public static string WithDateRange(string url, DateTime? from, DateTime? to)
    {
        var query = new List<string>();
        if (from.HasValue) query.Add($"from={from:yyyy-MM-dd}");
        if (to.HasValue) query.Add($"to={to:yyyy-MM-dd}");
        return query.Count == 0 ? url : url + "?" + string.Join("&", query);
    }
}
