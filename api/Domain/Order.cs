namespace Stock.Api.Domain;

public enum OrderStatus
{
    Pending = 1,
    Paid = 2,
    Cancelled = 3,
    Expired = 4
}

public class Order
{
    public long Id { get; set; }
    /// <summary>Random public identifier. Guests have no login, so this Guid is the only access key to an order.</summary>
    public Guid PublicId { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Total { get; set; }
    public string CustomerName { get; set; } = null!;
    public string CustomerEmail { get; set; } = null!;
    public string? CustomerPhone { get; set; }
    public string? ShippingAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public long OrderId { get; set; }
    public string Sku { get; set; } = null!;
    public string NameSnapshot { get; set; } = null!;
    public decimal UnitPriceSnapshot { get; set; }
    public int Quantity { get; set; }

    public Order Order { get; set; } = null!;
}
