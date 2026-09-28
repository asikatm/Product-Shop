using System.Text.Json;
using Microsoft.JSInterop;

namespace ProductShop.Client.Services;

public class CartLine
{
    public int VariantId { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public string? Image { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;

    public decimal Total => UnitPrice * Quantity;
}

// Website er cart - browser e (localStorage) thake, tai page reload e muche na
public class CartService
{
    private const string StorageKey = "shop.cart";
    public const int MaxQty = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IJSRuntime _js;
    private bool loaded;

    public CartService(IJSRuntime js)
    {
        _js = js;
    }

    public List<CartLine> Lines { get; private set; } = new();

    public int Count => Lines.Sum(l => l.Quantity);
    public decimal Total => Lines.Sum(l => l.Total);

    public event Action? Changed;

    public async Task LoadAsync()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(json))
                Lines = JsonSerializer.Deserialize<List<CartLine>>(json, JsonOptions) ?? new();
        }
        catch (JsonException)
        {
            Lines = new();
        }
        Changed?.Invoke();
    }

    public async Task AddAsync(CartLine line)
    {
        await LoadAsync();
        var existing = Lines.FirstOrDefault(l => l.VariantId == line.VariantId);
        if (existing != null)
        {
            existing.Quantity = Math.Min(MaxQty, existing.Quantity + line.Quantity);
            existing.UnitPrice = line.UnitPrice;
        }
        else
        {
            line.Quantity = Math.Clamp(line.Quantity, 1, MaxQty);
            Lines.Add(line);
        }
        await SaveAsync();
    }

    public async Task SetQuantityAsync(int variantId, int quantity)
    {
        var line = Lines.FirstOrDefault(l => l.VariantId == variantId);
        if (line == null) return;
        if (quantity <= 0) Lines.Remove(line);
        else line.Quantity = Math.Min(MaxQty, quantity);
        await SaveAsync();
    }

    public async Task RemoveAsync(int variantId) => await SetQuantityAsync(variantId, 0);

    public async Task ClearAsync()
    {
        Lines = new();
        await SaveAsync();
    }

    private async Task SaveAsync()
    {
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(Lines, JsonOptions));
        Changed?.Invoke();
    }
}
