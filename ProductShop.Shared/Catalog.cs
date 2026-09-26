using System.ComponentModel.DataAnnotations;

namespace ProductShop.Shared;

// Category ar Brand er moto shudhu naam wala list
public interface INamedItem
{
    int Id { get; set; }
    string Name { get; set; }
}

public class Category : INamedItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;
}

public class Brand : INamedItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;
}
