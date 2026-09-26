using ProductShop.Shared;

namespace ProductShop.Client.Helpers;

// Sale / Stock In form e product + size/color ek sathe select korar jonno
public record VariantOption(Product Product, ProductVariant Variant)
{
    public string Text => $"{Product.Name} — {Variant.Label}";

    public static Dictionary<int, VariantOption> Build(IEnumerable<Product> products) =>
        products.SelectMany(p => p.Variants.Select(v => new VariantOption(p, v)))
                .ToDictionary(o => o.Variant.Id);

    // Barcode scanner diye SKU dile oi variant khuje ber kore
    public static VariantOption? FindBySku(IEnumerable<VariantOption> options, string sku)
    {
        sku = sku.Trim();
        if (sku.Length == 0) return null;
        return options.FirstOrDefault(o => string.Equals(o.Variant.Sku, sku, StringComparison.OrdinalIgnoreCase));
    }
}
