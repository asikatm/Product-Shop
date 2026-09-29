using System.ComponentModel.DataAnnotations;

namespace ProductShop.Shared;

public static class WebOrderStatus
{
    public const string Pending = "Pending";      // website theke notun ashche
    public const string Confirmed = "Confirmed";  // phone e kotha bole confirm; sale (invoice) toiri, stock kome
    public const string Delivered = "Delivered";  // pouche geche, taka (COD) paoa geche
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Pending, Confirmed, Delivered, Cancelled };
}

// Website theke asha order
public class WebOrder
{
    public int Id { get; set; }

    // WEB-000001
    [StringLength(20)]
    public string OrderNo { get; set; } = string.Empty;

    [StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Note { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = WebOrderStatus.Pending;

    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal DeliveryCharge { get; set; }

    // SubTotal - Discount + DeliveryCharge
    public decimal Total { get; set; }

    // Customer age theke je taka diyeche (bKash ityadi) - confirm e invoice e paid hoy
    public decimal Advance { get; set; }

    // Courier ke je taka tulte hobe
    public decimal CodAmount => Math.Max(0, Total - Advance);

    public DateTime? DeliveredAt { get; set; }

    // Courier theke taka ashle je settlement e dhora hoyeche
    public int? CourierSettlementId { get; set; }

    // Confirm korle je sale / invoice toiri hoy
    public int? SaleId { get; set; }

    [StringLength(20)]
    public string? InvoiceNo { get; set; }

    [StringLength(200)]
    public string? CancelReason { get; set; }

    // Ke confirm / deliver / cancel koreche
    [StringLength(100)]
    public string? HandledBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    public List<WebOrderItem> Items { get; set; } = new();
}

public class WebOrderItem
{
    public int Id { get; set; }
    public int WebOrderId { get; set; }
    public int ProductId { get; set; }
    public int ProductVariantId { get; set; }

    [StringLength(100)]
    public string ProductName { get; set; } = string.Empty;

    [StringLength(60)]
    public string VariantName { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

// Confirm er age order bodlano: qty / dam / notun product, discount, advance
public class WebOrderUpdateRequest
{
    [Required(ErrorMessage = "Customer er naam dite hobe")]
    [StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number dite hobe")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Thikana dite hobe")]
    [StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Note { get; set; }

    public List<WebOrderLine> Items { get; set; } = new();

    public decimal Discount { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal Advance { get; set; }
}

// Order e notun product add korar list (kena dam nai)
public class OrderVariantOption
{
    public int VariantId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? Image { get; set; }
}

public class WebOrderLine
{
    public int VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class CancelWebOrderRequest
{
    [StringLength(200)]
    public string? Reason { get; set; }
}

// ---------- Website (public) er jonno ----------

// Kena dam / stock er sonkha website e jay na
public class StoreProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? BrandName { get; set; }
    public string? Fabric { get; set; }

    // Shobcheye kom dam er variant er MRP ar bikri dam
    public decimal Price { get; set; }
    public decimal FinalPrice { get; set; }

    public List<string> Images { get; set; } = new();
    public List<StoreVariant> Variants { get; set; } = new();

    public int DiscountPercent => Price > 0 && FinalPrice < Price ? (int)Math.Round((Price - FinalPrice) / Price * 100m) : 0;
}

public class StoreVariant
{
    public int Id { get; set; }
    public string Size { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal FinalPrice { get; set; }
    public bool InStock { get; set; }

    public string Label => string.IsNullOrWhiteSpace(Color) ? Size : $"{Size} / {Color}";
}

public class StoreCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProductCount { get; set; }

    // Category slider er gol chobi - ei category er notun product er prothom chobi
    public string? Image { get; set; }
}

public class StoreInfo
{
    public decimal DeliveryCharge { get; set; }
}

public class PlaceOrderItem
{
    public int VariantId { get; set; }
    public int Quantity { get; set; }
}

public class PlaceOrderRequest
{
    [Required(ErrorMessage = "Apnar naam likhun")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Naam 2-100 okkhor")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number likhun")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Shompurno thikana likhun")]
    [StringLength(300, MinimumLength = 5, ErrorMessage = "Thikana bistarito likhun")]
    public string Address { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Note { get; set; }

    public List<PlaceOrderItem> Items { get; set; } = new();
}

public class PlaceOrderResponse
{
    public string OrderNo { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

// Thank you page: order er obostha
public class OrderTrack
{
    public string OrderNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<WebOrderItem> Items { get; set; } = new();
}
