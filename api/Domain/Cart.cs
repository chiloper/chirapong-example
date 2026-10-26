namespace Stock.Api.Domain;

public class Cart
{
    /// <summary>Random Guid; it is also the guest's cookie value, so it must stay unguessable.</summary>
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<CartItem> Items { get; set; } = [];
}

public class CartItem
{
    public Guid CartId { get; set; }
    public string Sku { get; set; } = null!;
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; }

    public Cart Cart { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
